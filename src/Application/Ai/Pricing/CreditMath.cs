namespace Application.Ai.Pricing;

public static class CreditMath
{
    // Credits for units of a feature. Blocks are counted per request, or, with shared blocks, on the
    // units already paid with credits: a started block covers what fits in it
    public static int CreditsFor(FeaturePricing pricing, int units, int paidUnitsBefore = 0)
    {
        if (pricing.Credits is not { } perBlock || units <= 0) return 0;

        var block = Math.Max(1, pricing.CreditBlock);
        var before = pricing.SharedCreditBlocks ? paidUnitsBefore : 0;
        var blocks = Blocks(before + units, block) - Blocks(before, block);
        return blocks * perBlock;
    }

    private static int Blocks(int units, int block) => (units + block - 1) / block;
}
