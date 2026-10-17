// Isometric player controller: WASD/click movement and attack dispatch.
// Movement and combat are server-authoritative (native Unreal dedicated server).

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerController.h"
#include "Engine/NetSerialization.h"
#include "Core/MirTypes.h"
#include "MirPlayerController.generated.h"

class AMirIsometricCameraActor;

UCLASS()
class MIRUE_API AMirPlayerController : public APlayerController
{
	GENERATED_BODY()

public:
	AMirPlayerController();

	virtual void BeginPlay() override;
	virtual void SetupInputComponent() override;

	/** Trace from the cursor to the ground plane and return the world point. */
	UFUNCTION(BlueprintCallable, Category = "Mir|Input")
	bool GetCursorWorldLocation(FVector& OutLocation) const;

	/** Ask the server to walk the pawn toward a world location. */
	UFUNCTION(Server, Reliable, BlueprintCallable, Category = "Mir|Input")
	void ServerMoveTo(const FVector_NetQuantize& Location);

	/** Ask the server to perform a melee/ranged attack in the current facing. */
	UFUNCTION(Server, Reliable, BlueprintCallable, Category = "Mir|Input")
	void ServerAttack();

	/** Ask the server to pick up whatever is under the cursor. */
	UFUNCTION(Server, Reliable, BlueprintCallable, Category = "Mir|Input")
	void ServerPickup();

	UPROPERTY(EditDefaultsOnly, BlueprintReadWrite, Category = "Mir|Camera")
	TSubclassOf<AMirIsometricCameraActor> CameraActorClass;

protected:
	void OnMoveForward(float Value);
	void OnMoveRight(float Value);
	void OnAttackPressed();
	void OnPickupPressed();

private:
	UPROPERTY()
	TObjectPtr<AMirIsometricCameraActor> IsoCamera;
};
