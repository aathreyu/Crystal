// Renders a converted .map (see Tools/MirAssetConverter export_map_json) as
// isometric tile instances. Each tile layer (back/middle/front) samples a
// sprite atlas; per-instance custom data carries the sprite UV rect.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Core/MirTypes.h"
#include "MirMapActor.generated.h"

class UInstancedStaticMeshComponent;
class UMaterialInterface;

/** One decoded map cell (mirrors mir_assets.map_reader.MapCell.to_dict). */
USTRUCT(BlueprintType)
struct FMirMapCell
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Mir") int32 BackIndex = -1;
	UPROPERTY(BlueprintReadOnly, Category = "Mir") int32 BackImage = 0;
	UPROPERTY(BlueprintReadOnly, Category = "Mir") int32 MiddleIndex = -1;
	UPROPERTY(BlueprintReadOnly, Category = "Mir") int32 MiddleImage = 0;
	UPROPERTY(BlueprintReadOnly, Category = "Mir") int32 FrontIndex = -1;
	UPROPERTY(BlueprintReadOnly, Category = "Mir") int32 FrontImage = 0;
	UPROPERTY(BlueprintReadOnly, Category = "Mir") int32 Light = 0;
	UPROPERTY(BlueprintReadOnly, Category = "Mir") bool bHighWall = false;
};

UCLASS()
class MIRUE_API AMirMapActor : public AActor
{
	GENERATED_BODY()

public:
	AMirMapActor();

	/** Load a converted map JSON and rebuild all tile instances. */
	UFUNCTION(BlueprintCallable, CallInEditor, Category = "Mir|Map")
	void LoadMapFromJson(const FString& JsonPath);

	/** Convert a tile coordinate to world space on the ground plane. */
	UFUNCTION(BlueprintCallable, Category = "Mir|Map")
	FVector TileToWorld(int32 X, int32 Y) const;

	UFUNCTION(BlueprintCallable, Category = "Mir|Map")
	FIntPoint WorldToTile(const FVector& World) const;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir|Map")
	int32 MapWidth = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir|Map")
	int32 MapHeight = 0;

	/** World units per tile cell (defaults to the original 48x32). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir|Map")
	FVector2D CellSize = FVector2D(48.f, 32.f);

	/** Maps a cell's BackIndex/MiddleIndex/FrontIndex to a converted library folder. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir|Map")
	TMap<int32, FName> MapLibraryNames;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Mir|Map")
	TObjectPtr<UInstancedStaticMeshComponent> BackTiles;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Mir|Map")
	TObjectPtr<UInstancedStaticMeshComponent> MiddleTiles;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Mir|Map")
	TObjectPtr<UInstancedStaticMeshComponent> FrontTiles;

protected:
	/** Material with a per-instance UV rect param; see Docs/ASSET_PIPELINE.md. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "Mir|Map")
	TObjectPtr<UMaterialInterface> TileMaterial;

	UPROPERTY(BlueprintReadOnly, Category = "Mir|Map")
	TArray<FMirMapCell> Cells;

private:
	bool ParseMapJson(const FString& JsonPath);
	void AddTileLayer(UInstancedStaticMeshComponent* Layer, int32 X, int32 Y,
		int32 LibraryIndex, int32 ImageIndex, float HeightOffset);
};
