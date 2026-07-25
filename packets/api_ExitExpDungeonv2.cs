// /api/ExitExpDungeonv2  ReqPacket_ExitExpDungeon -> ResPacket_ExitExpDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitExpDungeon
{
    public int formationId;
    public int isWin;
    public int supportCharacterId;
    public bool supportParticipate;
    public BattlePassParameterFormat battlePassParameters;
    public List<DanteAbilityUsedLogFormat> danteAbilityUsageLogs;
    public int progressCount;
    public long PacketId;
}

public class ResPacket_ExitExpDungeon
{
    public int userExp;
    public List<StagePersonalityInfoFormat> personalityinfos;
    public List<Element> acquiredtickets;
    public List<Element> rewards;
    public ExpDungeonClearInfoFormat clearInfo;
    public List<Element> consumables;
    public long PacketId;
}

