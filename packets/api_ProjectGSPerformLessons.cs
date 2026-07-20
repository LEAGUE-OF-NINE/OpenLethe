// /api/ProjectGSPerformLessons  ReqPacket_ProjectGSPerformLessons -> ResPacket_ProjectGSPerformLessons
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ProjectGSPerformLessons
{
    public List<int> lessonIds;
    public long PacketId;
}

public class ResPacket_ProjectGSPerformLessons
{
    public int day;
    public ProjectGSGameData gameData;
    public ProjectGSLessonResultLog lessonResults;
    public List<int> comboIds;
    public List<int> discoveredComboIds;
    public long PacketId;
}

public class ProjectGSLessonResultLog
{
    public List<ProjectGSStats> formerStats;
    public List<LessonResultDetail> results;
}

public class LessonResultDetail
{
    public int lessonId;
    public int lessonLevel;
    public ProjectGSLessonResult result;
    public List<ProjectGSStats> statChanges;
    public int expGained;
    public bool leveledUp;
}

public enum ProjectGSLessonResult { CRITICAL_SUCCESS, SUCCESS, NORMAL, FAILURE, CRITICAL_FAILURE }

