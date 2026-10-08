// Where an evidence item is allowed to spawn. Each EvidenceSpawnPoint in the scene is tagged with one
// of these, and EvidenceData.allowedZones lists which zones an item may be placed in.
public enum EvidenceZone
{
    None,
    PharmacyCounter,
    BackStorage,
    Office,
    Entrance,
    StruggleArea,
    HiddenSpot,

    // Level 2 (the Marsh house). APPEND ONLY - this enum serializes as an int, so inserting a value
    // above this line silently re-points every existing EvidenceData asset's allowedZones.
    Foyer,
    Garage,
    Bedroom,
    Study,
    Driveway,
    Mudroom,
    Workshop,
    Desk,
    Storage,

    // Level 3 (the Penrose house and Ruth Calloway's next door). Same rule: append only.
    PenroseHall,
    BackRoom,
    Yard,
    NeighbourHouse,
    Loft,

    // Level 2's two neighbouring houses (east of the Marsh house, and the one behind it). Append only.
    EastHouse,
    BackHouse,
}

public static class EvidenceZoneText
{
    // Used in the stuck-hint line - phrased to slot into "Something doesn't add up ____."
    public static string HintPhrase(EvidenceZone zone)
    {
        switch (zone)
        {
            case EvidenceZone.PharmacyCounter: return "around the pharmacy counter";
            case EvidenceZone.BackStorage: return "in the back storage areas";
            case EvidenceZone.Office: return "in one of the offices";
            case EvidenceZone.Entrance: return "near the entrance";
            case EvidenceZone.StruggleArea: return "where the struggle happened";
            case EvidenceZone.HiddenSpot: return "somewhere nearby";

            case EvidenceZone.Foyer: return "by the front entrance";
            case EvidenceZone.Garage: return "out in the garage";
            case EvidenceZone.Bedroom: return "upstairs in the bedroom";
            case EvidenceZone.Study: return "in the study";
            case EvidenceZone.Driveway: return "out on the driveway";
            case EvidenceZone.Mudroom: return "by the mudroom door";
            case EvidenceZone.Workshop: return "in the workshop";
            case EvidenceZone.Desk: return "around the desk";
            case EvidenceZone.Storage: return "in the storage space";

            case EvidenceZone.PenroseHall: return "in the front hall";
            case EvidenceZone.BackRoom: return "in the back room";
            case EvidenceZone.Yard: return "out in the yard";
            case EvidenceZone.NeighbourHouse: return "in the house next door";
            case EvidenceZone.Loft: return "up in the loft next door";

            case EvidenceZone.EastHouse: return "in the house next door";
            case EvidenceZone.BackHouse: return "in the house out back";

            default: return "somewhere nearby";
        }
    }
}
