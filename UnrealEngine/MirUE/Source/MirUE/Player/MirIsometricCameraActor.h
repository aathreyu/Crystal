// Fixed isometric camera matching the original 48x32 tile projection.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "MirIsometricCameraActor.generated.h"

class USpringArmComponent;
class UCameraComponent;

UCLASS()
class MIRUE_API AMirIsometricCameraActor : public AActor
{
	GENERATED_BODY()

public:
	AMirIsometricCameraActor();

	/** Smoothly follow this world location (e.g. the player pawn). */
	UFUNCTION(BlueprintCallable, Category = "Mir|Camera")
	void SetFollowTarget(AActor* Target);

	UFUNCTION(BlueprintCallable, Category = "Mir|Camera")
	void SnapToTarget();

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir|Camera")
	float FollowSpeed = 8.f;

	/** Orthographic half-width in world units (bigger = more visible). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir|Camera")
	float OrthoWidth = 1600.f;

protected:
	virtual void Tick(float DeltaSeconds) override;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Mir|Camera")
	TObjectPtr<USpringArmComponent> SpringArm;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Mir|Camera")
	TObjectPtr<UCameraComponent> Camera;

private:
	UPROPERTY()
	TObjectPtr<AActor> FollowTarget;
};
