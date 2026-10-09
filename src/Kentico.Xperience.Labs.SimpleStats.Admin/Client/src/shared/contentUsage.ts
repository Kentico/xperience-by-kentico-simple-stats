/**
 * What counts as a usage of a reusable item. Shared by Content inventory ("Unused reusable items") and Reusable content usage,
 * which use the same definition on the server (`StatsContentUsageSql`).
 */
export const usageHint =
  'An item counts as used when another content item references it, in any language or version: through the content item selector or rich text editor (in content type fields or Page and Email Builder component properties), or through custom components with a reference extractor. References that exist only in code are not tracked.';