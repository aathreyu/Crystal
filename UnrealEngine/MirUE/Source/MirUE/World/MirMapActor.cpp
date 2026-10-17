#include "World/MirMapActor.h"

#include "Assets/MirAssetSubsystem.h"
#include "Components/InstancedStaticMeshComponent.h"
#include "Materials/MaterialInterface.h"
#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "Misc/FileHelper.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "Dom/JsonObject.h"
#include "Dom/JsonValue.h"
#include "UObject/ConstructorHelpers.h"

AMirMapActor::AMirMapActor()
{
	PrimaryActorTick.bCanEverTick = false;

	USceneComponent* Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);

	BackTiles = CreateDefaultSubobject<UInstancedStaticMeshComponent>(TEXT("BackTiles"));
	BackTiles->SetupAttachment(Root);
	BackTiles->NumCustomDataFloats = 4; // UVMin.xy, UVMax.xy
	BackTiles->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	MiddleTiles = CreateDefaultSubobject<UInstancedStaticMeshComponent>(TEXT("MiddleTiles"));
	MiddleTiles->SetupAttachment(Root);
	MiddleTiles->NumCustomDataFloats = 4;
	MiddleTiles->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	FrontTiles = CreateDefaultSubobject<UInstancedStaticMeshComponent>(TEXT("FrontTiles"));
	FrontTiles->SetupAttachment(Root);
	FrontTiles->NumCustomDataFloats = 4;
	FrontTiles->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	static ConstructorHelpers::FObjectFinder<UStaticMesh> PlaneMesh(
		TEXT("/Engine/BasicShapes/Plane.Plane"));
	if (PlaneMesh.Succeeded())
	{
		BackTiles->SetStaticMesh(PlaneMesh.Object);
		MiddleTiles->SetStaticMesh(PlaneMesh.Object);
		FrontTiles->SetStaticMesh(PlaneMesh.Object);
	}
}

FVector AMirMapActor::TileToWorld(int32 X, int32 Y) const
{
	return FVector(
		(X - Y) * (CellSize.X * 0.5f),
		(X + Y) * (CellSize.Y * 0.5f),
		0.f);
}

FIntPoint AMirMapActor::WorldToTile(const FVector& World) const
{
	const float A = World.X / (CellSize.X * 0.5f);
	const float B = World.Y / (CellSize.Y * 0.5f);
	return FIntPoint(
		FMath::RoundToInt((A + B) * 0.5f),
		FMath::RoundToInt((B - A) * 0.5f));
}

void AMirMapActor::LoadMapFromJson(const FString& JsonPath)
{
	if (!ParseMapJson(JsonPath))
	{
		return;
	}

	BackTiles->ClearInstances();
	MiddleTiles->ClearInstances();
	FrontTiles->ClearInstances();

	for (int32 X = 0; X < MapWidth; ++X)
	{
		for (int32 Y = 0; Y < MapHeight; ++Y)
		{
			const FMirMapCell& Cell = Cells[X * MapHeight + Y];
			AddTileLayer(BackTiles, X, Y, Cell.BackIndex, Cell.BackImage, 0.f);
			AddTileLayer(MiddleTiles, X, Y, Cell.MiddleIndex, Cell.MiddleImage, 1.f);
			AddTileLayer(FrontTiles, X, Y, Cell.FrontIndex, Cell.FrontImage, 2.f);
		}
	}

	UE_LOG(LogTemp, Log, TEXT("[Mir] Loaded map %dx%d (%d cells)"),
		MapWidth, MapHeight, Cells.Num());
}

void AMirMapActor::AddTileLayer(UInstancedStaticMeshComponent* Layer, int32 X, int32 Y,
	int32 LibraryIndex, int32 ImageIndex, float HeightOffset)
{
	if (!Layer || LibraryIndex < 0) return;

	const FName* LibraryName = MapLibraryNames.Find(LibraryIndex);
	if (!LibraryName) return;

	UGameInstance* GI = GetGameInstance();
	if (!GI) return;
	UMirAssetSubsystem* Assets = GI->GetSubsystem<UMirAssetSubsystem>();
	if (!Assets || !Assets->LoadLibrary(*LibraryName)) return;

	FVector4 UV;
	if (!Assets->GetSpriteUV(*LibraryName, ImageIndex, UV)) return;

	FVector2D Size(48.f, 32.f);
	Assets->GetSpriteSize(*LibraryName, ImageIndex, Size);

	const FVector Location = TileToWorld(X, Y) + FVector(0.f, 0.f, HeightOffset);
	// Plane.Plane is 100x100 uu; scale so a tile maps to its cell size.
	const FVector Scale(Size.X / 100.f, Size.Y / 100.f, 1.f);

	const int32 InstanceIndex = Layer->AddInstance(
		FTransform(FRotator::ZeroRotator, Location, Scale), /*bWorldSpace=*/true);

	Layer->SetCustomDataValue(InstanceIndex, 0, UV.X, false);
	Layer->SetCustomDataValue(InstanceIndex, 1, UV.Y, false);
	Layer->SetCustomDataValue(InstanceIndex, 2, UV.Z, false);
	Layer->SetCustomDataValue(InstanceIndex, 3, UV.W, false);
}

bool AMirMapActor::ParseMapJson(const FString& JsonPath)
{
	FString JsonString;
	if (!FFileHelper::LoadFileToString(JsonString, *JsonPath))
	{
		UE_LOG(LogTemp, Error, TEXT("[Mir] Cannot read map %s"), *JsonPath);
		return false;
	}

	TSharedPtr<FJsonObject> Root;
	const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonString);
	if (!FJsonSerializer::Deserialize(Reader, Root) || !Root.IsValid())
	{
		UE_LOG(LogTemp, Error, TEXT("[Mir] Malformed map JSON %s"), *JsonPath);
		return false;
	}

	MapWidth = Root->GetIntegerField(TEXT("width"));
	MapHeight = Root->GetIntegerField(TEXT("height"));
	if (MapWidth <= 0 || MapHeight <= 0)
	{
		UE_LOG(LogTemp, Error, TEXT("[Mir] Map has invalid size %dx%d"), MapWidth, MapHeight);
		return false;
	}

	const TArray<TSharedPtr<FJsonValue>>* Columns = nullptr;
	if (!Root->TryGetArrayField(TEXT("cells"), Columns) || Columns->Num() != MapWidth)
	{
		UE_LOG(LogTemp, Error, TEXT("[Mir] Map JSON cell columns mismatch"));
		return false;
	}

	Cells.Reset();
	Cells.SetNumZeroed(MapWidth * MapHeight);

	for (int32 X = 0; X < MapWidth; ++X)
	{
		const TArray<TSharedPtr<FJsonValue>>& Column = (*Columns)[X]->AsArray();
		if (Column.Num() != MapHeight)
		{
			UE_LOG(LogTemp, Error, TEXT("[Mir] Map column %d has %d rows, expected %d"),
				X, Column.Num(), MapHeight);
			return false;
		}

		for (int32 Y = 0; Y < MapHeight; ++Y)
		{
			const TSharedPtr<FJsonObject> Obj = Column[Y]->AsObject();
			if (!Obj.IsValid()) continue;

			FMirMapCell Cell;
			Cell.BackIndex = Obj->GetIntegerField(TEXT("backIndex"));
			Cell.BackImage = Obj->GetIntegerField(TEXT("backImage"));
			Cell.MiddleIndex = Obj->GetIntegerField(TEXT("middleIndex"));
			Cell.MiddleImage = Obj->GetIntegerField(TEXT("middleImage"));
			Cell.FrontIndex = Obj->GetIntegerField(TEXT("frontIndex"));
			Cell.FrontImage = Obj->GetIntegerField(TEXT("frontImage"));
			Cell.Light = Obj->GetIntegerField(TEXT("light"));
			Obj->TryGetBoolField(TEXT("highWall"), Cell.bHighWall);

			Cells[X * MapHeight + Y] = Cell;
		}
	}

	return true;
}
