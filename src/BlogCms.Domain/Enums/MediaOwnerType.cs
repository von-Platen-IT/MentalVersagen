namespace BlogCms.Domain.Enums;

/// <summary>
/// Polymorphic discriminator describing what a <c>MediaAsset</c> or
/// <c>VideoEmbed</c> belongs to.
/// </summary>
public enum MediaOwnerType
{
    Article,
    Comment
}
