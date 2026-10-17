#include "Player/MirPlayerController.h"

#include "Player/MirCharacter.h"
#include "Player/MirIsometricCameraActor.h"
#include "Engine/World.h"
#include "GameFramework/Pawn.h"

AMirPlayerController::AMirPlayerController()
{
	bShowMouseCursor = true;
	bEnableClickEvents = true;
	bEnableMouseOverEvents = true;
	CameraActorClass = AMirIsometricCameraActor::StaticClass();
}

void AMirPlayerController::BeginPlay()
{
	Super::BeginPlay();

	// Only the local player spawns/controls the isometric camera.
	if (!IsLocalController())
	{
		return;
	}

	UWorld* World = GetWorld();
	if (!World)
	{
		return;
	}

	FActorSpawnParameters Params;
	Params.Owner = this;
	IsoCamera = World->SpawnActor<AMirIsometricCameraActor>(
		CameraActorClass ? CameraActorClass : AMirIsometricCameraActor::StaticClass(),
		FTransform::Identity, Params);

	if (IsoCamera)
	{
		IsoCamera->SetFollowTarget(GetPawn());
		SetViewTarget(IsoCamera);
	}
}

void AMirPlayerController::SetupInputComponent()
{
	Super::SetupInputComponent();
	if (!InputComponent) return;

	InputComponent->BindAxis(TEXT("MoveForward"), this, &AMirPlayerController::OnMoveForward);
	InputComponent->BindAxis(TEXT("MoveRight"), this, &AMirPlayerController::OnMoveRight);
	InputComponent->BindAction(TEXT("MirAttack"), IE_Pressed, this, &AMirPlayerController::OnAttackPressed);
	InputComponent->BindAction(TEXT("MirPickup"), IE_Pressed, this, &AMirPlayerController::OnPickupPressed);
}

bool AMirPlayerController::GetCursorWorldLocation(FVector& OutLocation) const
{
	FHitResult Hit;
	if (GetHitResultUnderCursor(ECC_Visibility, false, Hit))
	{
		OutLocation = Hit.ImpactPoint;
		return true;
	}

	// Fallback: intersect the cursor ray with the z=0 ground plane.
	FVector Origin, Direction;
	if (DeprojectMousePositionToWorld(Origin, Direction) && !FMath::IsNearlyZero(Direction.Z))
	{
		const float T = -Origin.Z / Direction.Z;
		if (T > 0.f)
		{
			OutLocation = Origin + Direction * T;
			return true;
		}
	}
	return false;
}

void AMirPlayerController::OnMoveForward(float Value)
{
	if (FMath::IsNearlyZero(Value)) return;
	AMirCharacter* Mir = Cast<AMirCharacter>(GetPawn());
	if (!Mir) return;

	// Move along the camera's isometric "up" axis (screen up-right).
	const FVector Dir = FVector(1.f, 1.f, 0.f).GetSafeNormal();
	ServerMoveTo(Mir->GetActorLocation() + Dir * Value * 100.f);
}

void AMirPlayerController::OnMoveRight(float Value)
{
	if (FMath::IsNearlyZero(Value)) return;
	AMirCharacter* Mir = Cast<AMirCharacter>(GetPawn());
	if (!Mir) return;

	const FVector Dir = FVector(1.f, -1.f, 0.f).GetSafeNormal();
	ServerMoveTo(Mir->GetActorLocation() + Dir * Value * 100.f);
}

void AMirPlayerController::OnAttackPressed()
{
	ServerAttack();
}

void AMirPlayerController::OnPickupPressed()
{
	ServerPickup();
}

void AMirPlayerController::ServerMoveTo_Implementation(const FVector_NetQuantize& Location)
{
	AMirCharacter* Mir = Cast<AMirCharacter>(GetPawn());
	if (!Mir) return;

	// Snap to the tile grid and face the movement direction.
	const FVector Current = Mir->GetActorLocation();
	FVector Delta = Location - Current;
	Delta.Z = 0.f;

	if (!Delta.IsNearlyZero())
	{
		Mir->SetDirection(MirIso::WorldDeltaToDirection(FVector2D(Delta.X, Delta.Y)));
		Mir->SetAction(EMirAction::Walking);
		Mir->AddMovementInput(Delta.GetSafeNormal(), 1.f);
	}
}

void AMirPlayerController::ServerAttack_Implementation()
{
	if (AMirCharacter* Mir = Cast<AMirCharacter>(GetPawn()))
	{
		Mir->SetAction(EMirAction::Attack1);
	}
}

void AMirPlayerController::ServerPickup_Implementation()
{
	// Item pickup resolution belongs in the game mode / world actor.
}
