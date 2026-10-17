#include "Game/MirPlayerState.h"
#include "Net/UnrealNetwork.h"

void AMirPlayerState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);

	DOREPLIFETIME(AMirPlayerState, CharacterName);
	DOREPLIFETIME(AMirPlayerState, Class);
	DOREPLIFETIME(AMirPlayerState, Gender);
	DOREPLIFETIME(AMirPlayerState, Level);
	DOREPLIFETIME(AMirPlayerState, Experience);
	DOREPLIFETIME(AMirPlayerState, CurrentHP);
	DOREPLIFETIME(AMirPlayerState, CurrentMP);
	DOREPLIFETIME(AMirPlayerState, AttackMode);
	DOREPLIFETIME(AMirPlayerState, BodyLibrary);
	DOREPLIFETIME(AMirPlayerState, BodyIndex);
}
