namespace CBS.Domain.Enums;

public enum LcType
{
    ImportSight = 1,
    ImportUsance = 2,
    BackToBack = 3,
    ExportMaster = 4
}

public enum LcStatus
{
    Draft = 1,
    SanctionChecked = 2,
    Issued = 3,
    BillLodged = 4,
    Retired = 5,
    Cancelled = 6
}

public enum ShippingGuaranteeStatus
{
    Active = 1,
    ReleasedUponBlSurrender = 2,
    Cancelled = 3
}

public enum ExpTrackingStatus
{
    InTransit = 1,
    Realized = 2,
    OverdueReportedToBangladeshBank = 3
}

public enum Incoterm
{
    FOB = 1,
    CFR = 2,
    CIF = 3,
    EXW = 4,
    FCA = 5,
    CPT = 6,
    CIP = 7,
    DAP = 8,
    DPU = 9,
    DDP = 10
}

public enum TbmlRiskRating
{
    Low = 1,
    Medium = 2,
    High = 3,
    ExtremeHit = 4
}
