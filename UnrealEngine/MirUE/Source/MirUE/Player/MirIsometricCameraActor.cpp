#include "Player/MirIsometricCameraActor.h"

#include "Camera/CameraComponent.h"
#include "GameFramework/SpringArmComponent.h"

AMirIsometricCameraActor::AMirIsometricCameraActor()
{
	PrimaryActorTick.bCanEverTick = true;

	USceneComponent* Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);

	SpringArm = CreateDefaultSubobject<USpringArmComponent>(TEXT("SpringArm"));
	SpringArm->SetupAttachment(Root);
	SpringArm->TargetArmLength = 4000.f;
	SpringArm->bDoCollisionTest = false;
	SpringArm->bUsePawnControlRotation = false;
	SpringArm->SetUsingAbsoluteRotation(true);

	// Isometric angles: yaw 45 deg, pitch = -atan(CellHeight / CellWidth)
	// = -atan(32/48) ~= -33.69 deg, matching the original 48x32 grid.
	SpringArm->SetRelativeRotation(FRotator(-33.69f, -45.f, 0.f));

	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(SpringArm, USpringArmComponent::SocketName);
	Camera->ProjectionMode = ECameraProjectionMode::Orthographic;
	Camera->OrthoWidth = OrthoWidth;
	Camera->bUsePawnControlRotation = false;
}

void AMirIsometricCameraActor::SetFollowTarget(AActor* Target)
{
	FollowTarget = Target;
	SnapToTarget();
}

void AMirIsometricCameraActor::SnapToTarget()
{
	if (FollowTarget)
	{
		SetActorLocation(FollowTarget->GetActorLocation());
	}
}

void AMirIsometricCameraActor::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	if (Camera)
	{
		Camera->OrthoWidth = OrthoWidth;
	}

	if (FollowTarget)
	{
		const FVector Target = FollowTarget->GetActorLocation();
		const FVector Current = GetActorLocation();
		SetActorLocation(FMath::VInterpTo(Current, Target, DeltaSeconds, FollowSpeed));
	}
}
