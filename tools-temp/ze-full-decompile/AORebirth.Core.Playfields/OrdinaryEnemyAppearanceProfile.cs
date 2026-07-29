namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyAppearanceProfile
{
	internal int Side { get; private set; }

	internal int Fatness { get; private set; }

	internal int Breed { get; private set; }

	internal int Sex { get; private set; }

	internal int Race { get; private set; }

	internal int CharacterFlags { get; private set; }

	internal int AccountFlags { get; private set; }

	internal int Expansions { get; private set; }

	internal int NpcFamily { get; private set; }

	internal int NpcLosHeight { get; private set; }

	internal int VisualFlags { get; private set; }

	internal int VisibleTitle { get; private set; }

	internal uint AppearanceValue { get; private set; }

	internal int HeadMesh { get; private set; }

	internal bool ReplaceTextures { get; private set; }

	internal bool ClearTemplateHeadWhenZero { get; private set; }

	internal OrdinaryEnemyTextureProfile[] Textures { get; private set; }

	internal OrdinaryEnemyMeshProfile[] Meshes { get; private set; }

	internal OrdinaryEnemyScfuProfile ScfuProfile { get; private set; }

	internal OrdinaryEnemyAppearanceProfile(int side, int fatness, int breed, int sex, int race, int characterFlags, int accountFlags, int expansions, int npcFamily, int npcLosHeight, int visualFlags, int visibleTitle, uint appearanceValue, int headMesh, bool replaceTextures, bool clearTemplateHeadWhenZero, OrdinaryEnemyTextureProfile[] textures, OrdinaryEnemyMeshProfile[] meshes, OrdinaryEnemyScfuProfile scfuProfile)
	{
		Side = side;
		Fatness = fatness;
		Breed = breed;
		Sex = sex;
		Race = race;
		CharacterFlags = characterFlags;
		AccountFlags = accountFlags;
		Expansions = expansions;
		NpcFamily = npcFamily;
		NpcLosHeight = npcLosHeight;
		VisualFlags = visualFlags;
		VisibleTitle = visibleTitle;
		AppearanceValue = appearanceValue;
		HeadMesh = headMesh;
		ReplaceTextures = replaceTextures;
		ClearTemplateHeadWhenZero = clearTemplateHeadWhenZero;
		Textures = textures ?? new OrdinaryEnemyTextureProfile[0];
		Meshes = meshes ?? new OrdinaryEnemyMeshProfile[0];
		ScfuProfile = scfuProfile;
	}
}
