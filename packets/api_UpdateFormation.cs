// /api/UpdateFormation  ReqPacket_UpdateFormation -> ResPacket_UpdateFormation
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateFormation
{
    public FormationFormat formation;
    public FormationOrdersFormat formationOrders;
    public long PacketId;
}

public class ResPacket_UpdateFormation
{
    public long PacketId;
}

public class FormationFormat
{
    public int id;
    public List<FormationDetailFormat> formationDetails;
    public List<FormationNameElement> formationNameFormat;
}

public class FormationOrdersFormat
{
    public List<int> orders;
}

public class FormationDetailFormat
{
    public int personalityId;
    public Il2CppStructArray<int> egos;
    public bool isParticipated;
    public int participationOrder;
    public int skinId;
}

public class FormationNameElement
{
    public int k;
    public int v;
}

