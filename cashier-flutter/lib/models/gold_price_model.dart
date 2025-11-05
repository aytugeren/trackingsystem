class GoldPriceModel {
  final String name;
  final double alisFiyati;
  final double satisFiyati;
  final double ayar; // örn: 24, 22, 14

  const GoldPriceModel({
    required this.name,
    required this.alisFiyati,
    required this.satisFiyati,
    required this.ayar,
  });

  // 1️⃣ Milyem = (ayar / 24) * 1000
  double get milyem {
    final v = (ayar / 24.0) * 1000.0;
    return double.parse(v.toStringAsFixed(2));
  }

  // 2️⃣ Kâr Milyemi = (satis / alis) * milyem
  double get karMilyem {
    if (alisFiyati <= 0) return 0.0;
    final v = (satisFiyati / alisFiyati) * milyem;
    return double.parse(v.toStringAsFixed(2));
  }

  // 3️⃣ Kâr Oranı (%) = ((satis - alis) / alis) * 100
  double get karOraniYuzde {
    if (alisFiyati <= 0) return 0.0;
    final v = ((satisFiyati - alisFiyati) / alisFiyati) * 100.0;
    return double.parse(v.toStringAsFixed(2));
  }

  // Formatlı metinler (virgülden sonra 2 basamak)
  String get milyemText => milyem.toStringAsFixed(2);
  String get karMilyemText => karMilyem.toStringAsFixed(2);
  String get karOraniText => '%${karOraniYuzde.toStringAsFixed(2)}';
}
