namespace SysBot.Pokemon
{
    public enum PokeEvents
    {
        None,
        ManaphyEgg,
        BirthDayHappiny,
        PokeCenterPiplup,
        KorDawnPiplup,
        KorRegigigas,
        OtsukimiClefairy,
    }
    
    public enum RNGType
	{
        Wild,
        Starter,
        Legendary,
        Mew_or_Jirachi,
        Shamin,
        MysteryGift,
        Roamer,
        Egg,
        Underground,
        Gift,
        Gift_3IV,
        Custom,
	}

    public enum AutoRNGMode
	{
        AutoCalc,
        ExternalCalc,
	}

    public enum RNGRoutine
	{
        DelayCalc,
        LogAdvances,
        AutoRNG,
        Generator,
        CheckAvailablePKM,
    }

    public enum CheckMode
	{
        Box1Slot1,
        TeamSlot1,
        TeamSlot2,
        Wild,
        Seed,
	}

    public enum WildMode
	{
        None,
        Grass,
        Surf,
        Swarm,
        OldRod,
        GoodRod,
        SuperRod,
        Underground,
	}

    public enum GameTime
	{
        Morning = 0, //4am-10am
        Day = 1, //10am-5pm
        Sunset = 2, //5pm-8pm
        Night = 3, //8pm-2am
        DeepNight = 4, //2am-4am
	}

    public enum Compatibility
    {
        Low = 20,
        Mid = 50,
        High = 70,
    }

    public enum Lead
    {
        None = 0,
        CompoundEyes = 1,
        CuteCharmF = 2,
        CuteCharmM = 3,
        Pressure = 4,
    }

    public enum GenderRatio
    {
        Genderless,
        MaleOnly,
        FemaleOnly,
        M1F1,
        M1F3,
        M3F1,
        M7F1,

    }
    public enum AbilityNumber
    {
        Nofilter = 0,
        Ability1 = 1,
        Ability2 = 2,
        HiddenAbility = 4,
    }
}