namespace ZoneEngine_New.Core.Characters
{
    public interface ICharacterHydrationService
    {
        CharacterHydrationResult? LoadForLogin(int characterId);
    }
}
