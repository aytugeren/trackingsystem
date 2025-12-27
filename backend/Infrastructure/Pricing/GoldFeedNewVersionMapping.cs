using System.Collections.Generic;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

public sealed record GoldFeedIndexDefinition(int Index, string Label, bool IsUsed);

public static class GoldFeedNewVersionMapping
{
    public static readonly IReadOnlyList<GoldFeedIndexDefinition> Indexes = new[]
    {
        new GoldFeedIndexDefinition(1, "İAB Alış", true),
        new GoldFeedIndexDefinition(2, "22 Ayar Gram Alış", true),
        new GoldFeedIndexDefinition(3, "22 Ayar Gram Satış", true),
        new GoldFeedIndexDefinition(4, "KULLANILMAZ", false),
        new GoldFeedIndexDefinition(5, "KULLANILMAZ", false),
        new GoldFeedIndexDefinition(6, "18 Ayar Alış", true),
        new GoldFeedIndexDefinition(7, "KULLANILMAZ", false),
        new GoldFeedIndexDefinition(8, "10 Ayar Alış", true),
        new GoldFeedIndexDefinition(9, "KULLANILMAZ", false),
        new GoldFeedIndexDefinition(10, "14 Ayar Alış", true),
        new GoldFeedIndexDefinition(11, "KULLANILMAZ", false),
        new GoldFeedIndexDefinition(12, "8 Ayar Alış", true),
        new GoldFeedIndexDefinition(13, "KULLANILMAZ", false),
        new GoldFeedIndexDefinition(14, "Cumhuriyet / Ata Alış", true),
        new GoldFeedIndexDefinition(15, "Cumhuriyet / Ata Satış", true),
        new GoldFeedIndexDefinition(16, "Reşat 5’li Alış", true),
        new GoldFeedIndexDefinition(17, "Reşat 5’li Satış", true),
        new GoldFeedIndexDefinition(18, "Reşat Alış", true),
        new GoldFeedIndexDefinition(19, "Reşat Satış", true),
        new GoldFeedIndexDefinition(20, "Ata 5’li Alış", true),
        new GoldFeedIndexDefinition(21, "Ata 5’li Satış", true),
        new GoldFeedIndexDefinition(22, "Ziynet 5’li Alış", true),
        new GoldFeedIndexDefinition(23, "Ziynet 5’li Satış", true),
        new GoldFeedIndexDefinition(24, "2.5’luk Eski Alış", true),
        new GoldFeedIndexDefinition(25, "2.5’luk Eski Satış", true),
        new GoldFeedIndexDefinition(26, "2.5’luk Yeni Alış", true),
        new GoldFeedIndexDefinition(27, "2.5’luk Yeni Satış", true),
        new GoldFeedIndexDefinition(28, "Çeyrek Eski Alış", true),
        new GoldFeedIndexDefinition(29, "Çeyrek Eski Satış", true),
        new GoldFeedIndexDefinition(30, "Çeyrek Yeni Alış", true),
        new GoldFeedIndexDefinition(31, "Çeyrek Yeni Satış", true),
        new GoldFeedIndexDefinition(32, "Yarım Eski Alış", true),
        new GoldFeedIndexDefinition(33, "Yarım Eski Satış", true),
        new GoldFeedIndexDefinition(34, "Yarım Yeni Alış", true),
        new GoldFeedIndexDefinition(35, "Yarım Yeni Satış", true),
        new GoldFeedIndexDefinition(36, "Ziynet Eski Alış", true),
        new GoldFeedIndexDefinition(37, "Ziynet Eski Satış", true),
        new GoldFeedIndexDefinition(38, "Ziynet Yeni Alış", true),
        new GoldFeedIndexDefinition(39, "Ziynet Yeni Satış", true),
        new GoldFeedIndexDefinition(40, "24 Ayar 1 Gr Alış", true),
        new GoldFeedIndexDefinition(41, "24 Ayar 1 Gr Satış", true),
        new GoldFeedIndexDefinition(42, "22 Ayar 1 Gr Alış", true),
        new GoldFeedIndexDefinition(43, "22 Ayar 1 Gr Satış", true),
        new GoldFeedIndexDefinition(44, "22 Ayar 0.25 Gr Alış", true),
        new GoldFeedIndexDefinition(45, "22 Ayar 0.25 Gr Satış", true),
        new GoldFeedIndexDefinition(46, "22 Ayar 0.5 Gr Alış", true),
        new GoldFeedIndexDefinition(47, "22 Ayar 0.5 Gr Satış", true)
    };
}
