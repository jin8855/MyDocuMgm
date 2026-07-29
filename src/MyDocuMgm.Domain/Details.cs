namespace MyDocuMgm.Domain;

public abstract class ContentDetailsBase
{
    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;
}

public sealed class PlaceDetails : ContentDetailsBase
{
    public string? Address { get; set; }
    public string? BusinessHours { get; set; }
    public string? ParkingInfo { get; set; }
    public string? RecommendedMenuOrSpot { get; set; }
}

public sealed class CookingDetails : ContentDetailsBase
{
    public int? Servings { get; set; }
    public int? PreparationMinutes { get; set; }
    public int? CookingMinutes { get; set; }
    public string? Difficulty { get; set; }
    public ICollection<CookingIngredient> Ingredients { get; set; } = [];
}

public sealed class CookingIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public int SortOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Quantity { get; set; }
    public string IngredientType { get; set; } = "부재료";
    public bool IsPrimary { get; set; }
    public string? Note { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public CookingDetails CookingDetails { get; set; } = null!;
}

public sealed class ExerciseDetails : ContentDetailsBase
{
    public string? TargetArea { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Difficulty { get; set; }
    public string? Equipment { get; set; }
}

public sealed class CleaningLaundryDetails : ContentDetailsBase
{
    public string? Target { get; set; }
    public string? Supplies { get; set; }
    public string? Precautions { get; set; }
}

public sealed class TravelDetails : ContentDetailsBase
{
    public string? Destination { get; set; }
    public string? BestSeason { get; set; }
    public string? Transportation { get; set; }
    public string? BudgetNote { get; set; }
}

public sealed class PhotoDetails : ContentDetailsBase
{
    public string? Camera { get; set; }
    public string? Lens { get; set; }
    public string? ShootingSettings { get; set; }
    public string? Location { get; set; }
}

public sealed class StudyDetails : ContentDetailsBase
{
    public string? Subject { get; set; }
    public string? LearningGoal { get; set; }
    public string? Resource { get; set; }
    public string? ReviewCycle { get; set; }
}

public sealed class ProductDetails : ContentDetailsBase
{
    public string? Brand { get; set; }
    public string? ModelName { get; set; }
    public decimal? Price { get; set; }
    public string? PurchasePlace { get; set; }
}

public sealed class PhoneComputerDetails : ContentDetailsBase
{
    public string? DeviceOrOs { get; set; }
    public string? AppOrProgram { get; set; }
    public string? Problem { get; set; }
    public string? Solution { get; set; }
}

public sealed class TipDetails : ContentDetailsBase
{
    public string? Situation { get; set; }
    public string? KeyPoint { get; set; }
    public string? Precautions { get; set; }
}

public sealed class OtherDetails : ContentDetailsBase
{
    public string? CustomLabel { get; set; }
    public string? AdditionalInfo { get; set; }
}
