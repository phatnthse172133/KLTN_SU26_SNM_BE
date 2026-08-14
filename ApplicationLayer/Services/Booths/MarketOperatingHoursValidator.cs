using ApplicationLayer.Exceptions;

namespace ApplicationLayer.Services.Booths;

public static class MarketOperatingHoursValidator
{
    public static void Validate(
        TimeOnly? marketOpen,
        TimeOnly? marketClose,
        TimeOnly? boothOpen,
        TimeOnly? boothClose)
    {
        if (!boothOpen.HasValue || !boothClose.HasValue)
            return;

        if (!marketOpen.HasValue || !marketClose.HasValue)
            return;

        var mOpen = marketOpen.Value;
        var mClose = marketClose.Value;
        var bOpen = boothOpen.Value;
        var bClose = boothClose.Value;

        var marketSchedule = FormatSchedule(mOpen, mClose);

        if (mOpen == mClose)
            return;

        var fieldErrors = new Dictionary<string, string[]>();

        bool valid;

        if (mOpen < mClose)
        {
            if (bOpen < bClose)
            {
                valid = bOpen >= mOpen && bClose <= mClose;
            }
            else
            {
                valid = false;
            }
        }
        else
        {
            if (bOpen < bClose)
            {
                valid = bOpen >= mOpen || bClose <= mClose;
            }
            else
            {
                valid = bOpen >= mOpen && bClose <= mClose;
            }
        }

        if (!valid)
        {
            if (bOpen < bClose && mOpen < mClose && (bOpen < mOpen || bClose > mClose))
            {
                if (bOpen < mOpen)
                    fieldErrors["openTime"] = new[]
                    {
                        $"Booth opening time {bOpen:HH:mm} is earlier than this market's opening time {mOpen:HH:mm}. " +
                        $"Choose a time within the market schedule: {marketSchedule}."
                    };
                else
                    fieldErrors["closeTime"] = new[]
                    {
                        $"Booth closing time {bClose:HH:mm} is later than this market's closing time {mClose:HH:mm}. " +
                        $"Choose a time within the market schedule: {marketSchedule}."
                    };
            }
            else if (bOpen < bClose && mOpen > mClose)
            {
                if (bOpen < mOpen && bClose > mClose)
                    fieldErrors["openTime"] = new[]
                    {
                        $"Booth schedule {FormatSchedule(bOpen, bClose)} falls outside the market schedule {marketSchedule}. " +
                        $"The booth must operate entirely within the market's hours."
                    };
                else if (bOpen < mOpen)
                    fieldErrors["openTime"] = new[]
                    {
                        $"Booth opening time {bOpen:HH:mm} is earlier than this market's opening time {mOpen:HH:mm}. " +
                        $"Choose a time within the market schedule: {marketSchedule}."
                    };
                else
                    fieldErrors["closeTime"] = new[]
                    {
                        $"Booth closing time {bClose:HH:mm} is later than this market's closing time {mClose:HH:mm}. " +
                        $"Choose a time within the market schedule: {marketSchedule}."
                    };
            }
            else if (bOpen > bClose && mOpen < mClose)
            {
                fieldErrors["openTime"] = new[]
                {
                    $"Booth schedule {FormatSchedule(bOpen, bClose)} spans midnight but the market schedule {marketSchedule} does not. " +
                    $"The booth must operate entirely within the market's hours."
                };
            }
            else
            {
                if (bOpen < mOpen)
                    fieldErrors["openTime"] = new[]
                    {
                        $"Booth opening time {bOpen:HH:mm} is earlier than this market's opening time {mOpen:HH:mm}. " +
                        $"Choose a time within the market schedule: {marketSchedule}."
                    };
                if (bClose > mClose)
                    fieldErrors["closeTime"] = new[]
                    {
                        $"Booth closing time {bClose:HH:mm} is later than this market's closing time {mClose:HH:mm}. " +
                        $"Choose a time within the market schedule: {marketSchedule}."
                    };
                if (fieldErrors.Count == 0)
                    fieldErrors["openTime"] = new[]
                    {
                        $"Booth schedule {FormatSchedule(bOpen, bClose)} falls outside the market schedule {marketSchedule}. " +
                        $"The booth must operate entirely within the market's hours."
                    };
            }

            throw AppException.Validation("Booth operating hours must fall within the market's operating hours.", fieldErrors, "BOOTH_OUTSIDE_MARKET_HOURS");
        }
    }

    public static string FormatSchedule(TimeOnly open, TimeOnly close)
        => open == close ? "24 hours" : $"{open:HH:mm}-{close:HH:mm}";
}
