namespace AutoReadMod
{
    /// <summary>
    /// Source of an auto-read: container (loot UI) or bundle (inventory from opening a bundle).
    /// </summary>
    public enum AutoReadVisualizationSource
    {
        Container,
        Bundle,
    }

    /// <summary>
    /// Payload for one auto-read visualization: item identity, count, placement hint, and kind for color-coding.
    /// Carries ItemValue when available so the overlay can AddItemStack without resolving by name.
    /// </summary>
    public sealed class AutoReadVisualizationEvent
    {
        public string ItemKey { get; }
        public string ItemClassName { get; }
        public string LocalizedItemName { get; }
        public string IconName { get; }
        public int Count { get; }
        public AutoReadVisualizationSource Source { get; }
        public AutoReader.ReadableItemKind Kind { get; }
        /// <summary>When non-null, use this to build ItemStack for AddItemStack (avoids name lookup).</summary>
        public ItemValue ItemValue { get; }

        public AutoReadVisualizationEvent(
            string itemKey,
            string itemClassName,
            string localizedItemName,
            string iconName,
            int count,
            AutoReadVisualizationSource source,
            AutoReader.ReadableItemKind kind,
            ItemValue itemValue = null)
        {
            ItemKey = itemKey ?? string.Empty;
            ItemClassName = itemClassName ?? string.Empty;
            LocalizedItemName = localizedItemName ?? string.Empty;
            IconName = iconName ?? string.Empty;
            Count = count > 0 ? count : 1;
            Source = source;
            Kind = kind;
            ItemValue = itemValue;
        }
    }
}
