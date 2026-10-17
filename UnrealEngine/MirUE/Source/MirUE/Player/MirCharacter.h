// Replicated player/hero pawn. Movement + action state are server-authoritative
// and drive a billboard sprite rendered from a converted .Lib atlas.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "Core/MirTypes.h"
#include "MirCharacter.generated.h"

class UStaticMeshComponent;
class UMaterialInterface;
class UMaterialInstanceDynamic;

UCLASS()
class MIRUE_API AMirCharacter : public ACharacter
{
	GENERATED_BODY()

public:
	AMirCharacter();

	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	/** Current facing; replicated so all clients agree. */
	UPROPERTY(ReplicatedUsing = OnRep_Direction, BlueprintReadOnly, Category = "Mir")
	EMirDirection Direction = EMirDirection::Down;

	/** Current animation action. */
	UPROPERTY(ReplicatedUsing = OnRep_Action, BlueprintReadOnly, Category = "Mir")
	EMirAction Action = EMirAction::Standing;

	/** Which converted library holds this body, e.g. "Monsters" or a class library. */
	UPROPERTY(Replicated, EditAnywhere, BlueprintReadWrite, Category = "Mir")
	FName BodyLibrary = TEXT("Monsters");

	/** Base image index inside BodyLibrary (mirrors Monster.BaseImage / Player shape). */
	UPROPERTY(Replicated, EditAnywhere, BlueprintReadWrite, Category = "Mir")
	int32 BodyIndex = 0;

	UFUNCTION(BlueprintCallable, Category = "Mir")
	void SetDirection(EMirDirection NewDirection);

	UFUNCTION(BlueprintCallable, Category = "Mir")
	void SetAction(EMirAction NewAction);

protected:
	/** Quad that displays the current sprite frame. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Mir")
	TObjectPtr<UStaticMeshComponent> SpriteQuad;

	/** Material with params SpriteTexture / UVMin / UVMax / FramesXY (see docs). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Mir")
	TObjectPtr<UMaterialInterface> SpriteMaterial;

	UPROPERTY()
	TObjectPtr<UMaterialInstanceDynamic> SpriteMID;

	/** Milliseconds per animation frame (from the .Lib FrameSet). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir")
	float FrameIntervalMs = 100.f;

	UFUNCTION()
	void OnRep_Direction();

	UFUNCTION()
	void OnRep_Action();

private:
	void RefreshSprite(float DeltaSeconds);
	void UpdateFacingRotation();

	/** FrameSet direction column, matching MirDirection ordering. */
	int32 CurrentFrame = 0;
	float FrameAccumulatorMs = 0.f;
};
