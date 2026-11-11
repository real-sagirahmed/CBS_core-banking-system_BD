namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: Multi-Account Sweep Classification (BRPD Circular 12/2024 Module 1.8.1-i, ii).
// Supports one-to-many surplus dispersal, many-to-one collection pooling,
// and algorithmic distribution strategies (Percentage vs Priority Waterfall).
public enum MultiSweepType
{
    OneToMany = 1,
    ManyToOne = 2
}

public enum AllocationStrategy
{
    PercentageSplit = 1,
    PriorityWaterfall = 2
}
