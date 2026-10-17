// Loads assets produced by Tools/MirAssetConverter at runtime.
//
// Each converted library directory contains `atlas.png` + `atlas.json`
// (see export_library_atlas). This subsystem reads them from
// `<ProjectContent>/MirData/<Library>/` and exposes textures + sprite UV rects
// so the tile renderer and character renderer can draw without hand-authored
// .uasset files.

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Core/MirTypes.h"
#include "MirAssetSubsystem.generated.h"

/** One sprite within an atlas, in pixels. */
USTRUCT(BlueprintType)
struct FMirSpriteRect
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	int32 Index = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	float X = 0.f;

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	float Y = 0.f;

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	float Width = 0.f;

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	float Height = 0.f;
};

/** A loaded library: atlas texture, sprite rects and animation frames. */
USTRUCT(BlueprintType)
struct FMirLibraryAtlas
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	TObjectPtr<UTexture2D> Texture = nullptr;

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	int32 AtlasWidth = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	int32 AtlasHeight = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	TMap<int32, FMirSpriteRect> Rects;

	UPROPERTY(BlueprintReadOnly, Category = "Mir")
	TMap<EMirAction, FMirAnimFrame> Frames;

	bool IsValid() const { return Texture != nullptr; }
};

UCLASS()
class MIRUE_API UMirAssetSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	/** Load (and cache) a library by folder name, e.g. "Monsters", "Items". */
	UFUNCTION(BlueprintCallable, Category = "Mir|Assets")
	bool LoadLibrary(const FName LibraryName);

	UFUNCTION(BlueprintCallable, Category = "Mir|Assets")
	UTexture2D* GetAtlasTexture(const FName LibraryName) const;

	/** UV rect (0..1) for a sprite index inside a library's atlas. */
	bool GetSpriteUV(const FName LibraryName, int32 Index, FVector4& OutUV) const;

	/** Pixel size of a sprite (for quad sizing). */
	bool GetSpriteSize(const FName LibraryName, int32 Index, FVector2D& OutSize) const;

	/** Animation frame definition for an action in a library. */
	bool GetFrame(const FName LibraryName, EMirAction Action, FMirAnimFrame& OutFrame) const;

	/** Root folder holding converted data; defaults to `<Content>/MirData`. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Mir|Assets")
	FString DataRoot;

private:
	FMirLibraryAtlas* FindLibrary(const FName LibraryName) const;
	bool LoadAtlasJSON(const FString& JsonPath, FMirLibraryAtlas& OutAtlas);

	UPROPERTY()
	TMap<FName, FMirLibraryAtlas> Libraries;
};
