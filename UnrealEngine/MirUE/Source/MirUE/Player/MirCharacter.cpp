#include "Player/MirCharacter.h"

#include "Assets/MirAssetSubsystem.h"
#include "Components/StaticMeshComponent.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Materials/MaterialInterface.h"
#include "UObject/ConstructorHelpers.h"
#include "Engine/GameInstance.h"
#include "Net/UnrealNetwork.h"

AMirCharacter::AMirCharacter()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = true;
	SetReplicateMovement(true);

	SpriteQuad = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("SpriteQuad"));
	SpriteQuad->SetupAttachment(RootComponent);
	SpriteQuad->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	SpriteQuad->SetCastShadow(false);

	// The billboard faces the camera: rotate the quad to stand up (Y-up plane).
	SpriteQuad->SetRelativeRotation(FRotator(-90.f, 0.f, 0.f));

	static ConstructorHelpers::FObjectFinder<UStaticMesh> PlaneMesh(
		TEXT("/Engine/BasicShapes/Plane.Plane"));
	if (PlaneMesh.Succeeded())
	{
		SpriteQuad->SetStaticMesh(PlaneMesh.Object);
	}

	// Sensible default; override in a Blueprint child.
	static ConstructorHelpers::FObjectFinder<UMaterialInterface> DefaultMat(
		TEXT("/Engine/EngineMaterials/DefaultMaterial.DefaultMaterial"));
	if (DefaultMat.Succeeded())
	{
		SpriteMaterial = DefaultMat.Object;
	}
}

void AMirCharacter::BeginPlay()
{
	Super::BeginPlay();

	if (SpriteMaterial)
	{
		SpriteMID = UMaterialInstanceDynamic::Create(SpriteMaterial, this);
		if (SpriteMID)
		{
			SpriteQuad->SetMaterial(0, SpriteMID);
		}
	}

	UpdateFacingRotation();
	RefreshSprite(0.f);
}

void AMirCharacter::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);

	DOREPLIFETIME(AMirCharacter, Direction);
	DOREPLIFETIME(AMirCharacter, Action);
	DOREPLIFETIME(AMirCharacter, BodyLibrary);
	DOREPLIFETIME(AMirCharacter, BodyIndex);
}

void AMirCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	RefreshSprite(DeltaSeconds);
}

void AMirCharacter::SetDirection(EMirDirection NewDirection)
{
	if (Direction == NewDirection) return;
	Direction = NewDirection;
	UpdateFacingRotation();
	RefreshSprite(0.f);
}

void AMirCharacter::SetAction(EMirAction NewAction)
{
	if (Action == NewAction) return;
	Action = NewAction;
	CurrentFrame = 0;
	FrameAccumulatorMs = 0.f;
	RefreshSprite(0.f);
}

void AMirCharacter::OnRep_Direction()
{
	UpdateFacingRotation();
	RefreshSprite(0.f);
}

void AMirCharacter::OnRep_Action()
{
	CurrentFrame = 0;
	FrameAccumulatorMs = 0.f;
	RefreshSprite(0.f);
}

void AMirCharacter::UpdateFacingRotation()
{
	// Mir sprites are pre-rendered per direction, so the quad never rotates.
	// (Kept as a hook for effects/mounts that do.)
}

void AMirCharacter::RefreshSprite(float DeltaSeconds)
{
	if (!SpriteMID) return;

	UGameInstance* GI = GetGameInstance();
	if (!GI) return;

	UMirAssetSubsystem* Assets = GI->GetSubsystem<UMirAssetSubsystem>();
	if (!Assets) return;

	if (!Assets->LoadLibrary(BodyLibrary)) return;

	FMirAnimFrame Frame;
	if (!Assets->GetFrame(BodyLibrary, Action, Frame))
	{
		Assets->GetFrame(BodyLibrary, EMirAction::Standing, Frame);
	}

	// Advance the animation clock.
	const float Interval = FrameIntervalMs > 0.f ? FrameIntervalMs : 100.f;
	FrameAccumulatorMs += DeltaSeconds * 1000.f;
	if (FrameAccumulatorMs >= Interval)
	{
		FrameAccumulatorMs = FMath::Fmod(FrameAccumulatorMs, Interval);
		CurrentFrame = (CurrentFrame + 1) % FMath::Max(1, Frame.Count);
	}

	// Sprite index = Start + direction * (Count + Skip) + frame, matching the
	// original engine's BodyLibrary.Draw(...) indexing.
	const int32 Offset = Frame.Count + Frame.Skip;
	const int32 SpriteIndex =
		Frame.Start + (static_cast<int32>(Direction) * Offset) + CurrentFrame;

	FVector4 UV;
	if (!Assets->GetSpriteUV(BodyLibrary, SpriteIndex, UV))
	{
		return;
	}

	SpriteMID->SetVectorParameterValue(TEXT("UVMin"), FLinearColor(UV.X, UV.Y, 0.f, 0.f));
	SpriteMID->SetVectorParameterValue(TEXT("UVMax"), FLinearColor(UV.Z, UV.W, 0.f, 0.f));

	if (UTexture2D* Texture = Assets->GetAtlasTexture(BodyLibrary))
	{
		SpriteMID->SetTextureParameterValue(TEXT("SpriteTexture"), Texture);
	}

	FVector2D Size(48.f, 64.f);
	Assets->GetSpriteSize(BodyLibrary, SpriteIndex, Size);
	// Plane.Plane is 100x100 uu; scale so 1 sprite pixel ~ 1 uu.
	SpriteQuad->SetRelativeScale3D(FVector(Size.X / 100.f, Size.Y / 100.f, 1.f));
}
