#include "Assets/MirAssetSubsystem.h"

#include "ImageUtils.h"
#include "Engine/Texture2D.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "Dom/JsonObject.h"
#include "Dom/JsonValue.h"

namespace
{
	// Maps the JSON frame keys emitted by the converter back to EMirAction.
	EMirAction FrameNameToAction(const FString& Name)
	{
		if (Name == TEXT("Standing")) return EMirAction::Standing;
		if (Name == TEXT("Walking")) return EMirAction::Walking;
		if (Name == TEXT("Running")) return EMirAction::Running;
		if (Name == TEXT("Attack1")) return EMirAction::Attack1;
		if (Name == TEXT("Attack2")) return EMirAction::Attack2;
		if (Name == TEXT("Attack3")) return EMirAction::Attack3;
		if (Name == TEXT("AttackRange1")) return EMirAction::AttackRange1;
		if (Name == TEXT("Struck")) return EMirAction::Struck;
		if (Name == TEXT("Spell")) return EMirAction::Spell;
		if (Name == TEXT("Die")) return EMirAction::Die;
		if (Name == TEXT("Dead")) return EMirAction::Dead;
		if (Name == TEXT("Appear")) return EMirAction::Appear;
		if (Name == TEXT("Revive")) return EMirAction::Revive;
		if (Name == TEXT("Mine")) return EMirAction::Mine;
		return EMirAction::Standing;
	}
}

void UMirAssetSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);

	if (DataRoot.IsEmpty())
	{
		DataRoot = FPaths::ProjectContentDir() / TEXT("MirData");
	}
}

void UMirAssetSubsystem::Deinitialize()
{
	Libraries.Empty();
	Super::Deinitialize();
}

FMirLibraryAtlas* UMirAssetSubsystem::FindLibrary(const FName LibraryName) const
{
	return Libraries.Find(LibraryName);
}

bool UMirAssetSubsystem::LoadLibrary(const FName LibraryName)
{
	if (Libraries.Contains(LibraryName))
	{
		return true;
	}

	const FString LibraryDir = DataRoot / LibraryName.ToString();
	const FString AtlasPath = LibraryDir / TEXT("atlas.png");
	const FString JsonPath = LibraryDir / TEXT("atlas.json");

	if (!FPaths::FileExists(AtlasPath) || !FPaths::FileExists(JsonPath))
	{
		UE_LOG(LogTemp, Warning, TEXT("[Mir] Library '%s' not found under %s"),
			*LibraryName.ToString(), *DataRoot);
		return false;
	}

	TArray<uint8> PngData;
	if (!FFileHelper::LoadFileToArray(PngData, *AtlasPath))
	{
		UE_LOG(LogTemp, Error, TEXT("[Mir] Failed to read atlas %s"), *AtlasPath);
		return false;
	}

	UTexture2D* Texture = FImageUtils::ImportBufferAsTexture2D(PngData);
	if (!Texture)
	{
		UE_LOG(LogTemp, Error, TEXT("[Mir] Failed to decode atlas %s"), *AtlasPath);
		return false;
	}
	Texture->SRGB = true;
	Texture->Filter = TextureFilter::TF_Nearest; // crisp 2D sprites
	Texture->UpdateResource();

	FMirLibraryAtlas Atlas;
	Atlas.Texture = Texture;
	if (!LoadAtlasJSON(JsonPath, Atlas))
	{
		return false;
	}

	Libraries.Add(LibraryName, MoveTemp(Atlas));
	return true;
}

UTexture2D* UMirAssetSubsystem::GetAtlasTexture(const FName LibraryName) const
{
	const FMirLibraryAtlas* Atlas = FindLibrary(LibraryName);
	return Atlas ? Atlas->Texture : nullptr;
}

bool UMirAssetSubsystem::LoadAtlasJSON(const FString& JsonPath, FMirLibraryAtlas& OutAtlas)
{
	FString JsonString;
	if (!FFileHelper::LoadFileToString(JsonString, *JsonPath))
	{
		UE_LOG(LogTemp, Error, TEXT("[Mir] Failed to read manifest %s"), *JsonPath);
		return false;
	}

	TSharedPtr<FJsonObject> Root;
	const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonString);
	if (!FJsonSerializer::Deserialize(Reader, Root) || !Root.IsValid())
	{
		UE_LOG(LogTemp, Error, TEXT("[Mir] Malformed manifest %s"), *JsonPath);
		return false;
	}

	OutAtlas.AtlasWidth = Root->GetIntegerField(TEXT("width"));
	OutAtlas.AtlasHeight = Root->GetIntegerField(TEXT("height"));

	const TArray<TSharedPtr<FJsonValue>>* Sprites = nullptr;
	if (Root->TryGetArrayField(TEXT("sprites"), Sprites))
	{
		for (const TSharedPtr<FJsonValue>& Value : *Sprites)
		{
			const TSharedPtr<FJsonObject> Obj = Value->AsObject();
			if (!Obj.IsValid()) continue;

			FMirSpriteRect Rect;
			Rect.Index = Obj->GetIntegerField(TEXT("index"));
			Rect.X = static_cast<float>(Obj->GetNumberField(TEXT("x")));
			Rect.Y = static_cast<float>(Obj->GetNumberField(TEXT("y")));
			Rect.Width = static_cast<float>(Obj->GetNumberField(TEXT("width")));
			Rect.Height = static_cast<float>(Obj->GetNumberField(TEXT("height")));
			OutAtlas.Rects.Add(Rect.Index, Rect);
		}
	}

	const TSharedPtr<FJsonObject>* Frames = nullptr;
	if (Root->TryGetObjectField(TEXT("frames"), Frames))
	{
		for (const auto& Pair : (*Frames)->Values)
		{
			const TSharedPtr<FJsonObject> FrameObj = Pair.Value->AsObject();
			if (!FrameObj.IsValid()) continue;

			FMirAnimFrame Frame;
			Frame.Action = FrameNameToAction(Pair.Key);
			Frame.Start = FrameObj->GetIntegerField(TEXT("start"));
			Frame.Count = FMath::Max(1, FrameObj->GetIntegerField(TEXT("count")));
			Frame.Skip = FrameObj->GetIntegerField(TEXT("skip"));
			Frame.Interval = FrameObj->GetIntegerField(TEXT("interval"));
			FrameObj->TryGetBoolField(TEXT("reverse"), Frame.bReverse);
			FrameObj->TryGetBoolField(TEXT("blend"), Frame.bBlend);
			OutAtlas.Frames.Add(Frame.Action, Frame);
		}
	}

	return true;
}

bool UMirAssetSubsystem::GetSpriteUV(const FName LibraryName, int32 Index, FVector4& OutUV) const
{
	const FMirLibraryAtlas* Atlas = FindLibrary(LibraryName);
	if (!Atlas || Atlas->AtlasWidth <= 0 || Atlas->AtlasHeight <= 0)
	{
		return false;
	}

	const FMirSpriteRect* Rect = Atlas->Rects.Find(Index);
	if (!Rect)
	{
		return false;
	}

	const float W = static_cast<float>(Atlas->AtlasWidth);
	const float H = static_cast<float>(Atlas->AtlasHeight);
	OutUV = FVector4(
		Rect->X / W,
		Rect->Y / H,
		(Rect->X + Rect->Width) / W,
		(Rect->Y + Rect->Height) / H);
	return true;
}

bool UMirAssetSubsystem::GetSpriteSize(const FName LibraryName, int32 Index, FVector2D& OutSize) const
{
	const FMirLibraryAtlas* Atlas = FindLibrary(LibraryName);
	if (!Atlas) return false;

	const FMirSpriteRect* Rect = Atlas->Rects.Find(Index);
	if (!Rect) return false;

	OutSize = FVector2D(Rect->Width, Rect->Height);
	return true;
}

bool UMirAssetSubsystem::GetFrame(const FName LibraryName, EMirAction Action, FMirAnimFrame& OutFrame) const
{
	const FMirLibraryAtlas* Atlas = FindLibrary(LibraryName);
	if (!Atlas) return false;

	const FMirAnimFrame* Frame = Atlas->Frames.Find(Action);
	if (!Frame) return false;

	OutFrame = *Frame;
	return true;
}
