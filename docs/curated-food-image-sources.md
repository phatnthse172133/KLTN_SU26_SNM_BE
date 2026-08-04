# Curated food image source report

Validation date: 2026-08-04. Provider: Wikimedia Commons. Google/image search was used only for discovery; the application stores no Google thumbnail or search-result URL.

The authoritative per-food mapping is the ordered `Foods`/`ImageFiles` mapping in `20260804023000_AddCuratedVietnameseNightMarketMenus.cs`. For every filename `F`:

- image URL: `https://commons.wikimedia.org/wiki/Special:Redirect/file/{url-encoded F}`;
- source page: `https://commons.wikimedia.org/wiki/File:{url-encoded F}`;
- attribution and license: the structured `Artist` and `LicenseShortName` fields on that source page.

Batch verification of the 100 mappings found 98 unique photographs. All 100 resolve to Commons file records after the three replacements in `20260804033000_ReplaceBrokenCuratedImageLinks`; all expose an `image/*` original URL and a license. License distribution: CC BY-SA 4.0 (35), CC BY-SA 2.0 (14), CC BY 2.0 (15), Public domain (12), CC BY-SA 3.0 (11), CC0 (10), CC BY 3.0 (2), CC BY 4.0 (1). The two reused photographs are limited to closely related vegetarian variants and require final product-owner approval.

Manual visual review is still required before treating the image set as production-cleared. Category membership and filenames verify the food family, but several variants do not establish the exact topping/preparation shown in the photo. In particular, review phở variants other than `Phở bò tái`, `Phở đặc biệt`, `Phở gà`, and `Phở chay`; the snack group; drink flavors; vegetarian variants; and individual grilled/seafood preparations. Copyright/license verification has passed; exact dish-to-photo verification is not claimed for those rows.

The migration contains no AI-generated image, placeholder, `encrypted-tbn0.gstatic.com`, Google thumbnail, base64 image, or expiring token URL.
