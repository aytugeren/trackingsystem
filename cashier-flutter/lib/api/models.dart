// Basic models and enums matching backend contracts

enum Role { Kasiyer, Yonetici }

enum OdemeSekli { Havale, KrediKarti }

enum AltinAyar { Ayar22, Ayar24 }

extension OdemeSekliX on OdemeSekli {
  int toInt() => this == OdemeSekli.Havale ? 0 : 1;
}

extension AltinAyarX on AltinAyar {
  int toInt() => this == AltinAyar.Ayar22 ? 22 : 24;
}

class LoginResponse {
  final String token;
  final String role;
  final String email;

  LoginResponse({required this.token, required this.role, required this.email});

  factory LoginResponse.fromJson(Map<String, dynamic> json) => LoginResponse(
        token: json['token'] as String,
        role: json['role'] as String,
        email: json['email'] as String,
      );
}

class CreateInvoiceDto {
  final DateTime tarih; // date only (yyyy-MM-dd)
  final int siraNo; // ignored by draft API; server assigns
  final String musteriAdSoyad;
  final String tckn;
  final double tutar;
  final OdemeSekli odemeSekli;
  final AltinAyar altinAyar;

  CreateInvoiceDto({
    required this.tarih,
    this.siraNo = 0,
    required this.musteriAdSoyad,
    required this.tckn,
    required this.tutar,
    required this.odemeSekli,
    required this.altinAyar,
  });

  Map<String, dynamic> toJson() => {
        'tarih': _toDateOnly(tarih),
        'siraNo': siraNo,
        'musteriAdSoyad': musteriAdSoyad,
        'tckn': tckn,
        'tutar': tutar,
        'odemeSekli': odemeSekli.toInt(),
        'altinAyar': altinAyar.toInt(),
      };
}

class CreateExpenseDto {
  final DateTime tarih; // date only (yyyy-MM-dd)
  final int siraNo; // ignored by draft API; server assigns
  final String musteriAdSoyad;
  final String tckn;
  final double tutar;
  final AltinAyar altinAyar;

  CreateExpenseDto({
    required this.tarih,
    this.siraNo = 0,
    required this.musteriAdSoyad,
    required this.tckn,
    required this.tutar,
    required this.altinAyar,
  });

  Map<String, dynamic> toJson() => {
        'tarih': _toDateOnly(tarih),
        'siraNo': siraNo,
        'musteriAdSoyad': musteriAdSoyad,
        'tckn': tckn,
        'tutar': tutar,
        'altinAyar': altinAyar.toInt(),
      };
}

String _toDateOnly(DateTime dt) {
  // backend expects DateOnly as yyyy-MM-dd
  final y = dt.year.toString().padLeft(4, '0');
  final m = dt.month.toString().padLeft(2, '0');
  final d = dt.day.toString().padLeft(2, '0');
  return '$y-$m-$d';
}

class CreatedDraftResponse {
  final String id;
  final int siraNo;
  final double? altinSatisFiyati;

  CreatedDraftResponse({required this.id, required this.siraNo, this.altinSatisFiyati});

  factory CreatedDraftResponse.fromJson(Map<String, dynamic> json) => CreatedDraftResponse(
        id: json['id'] as String,
        siraNo: (json['siraNo'] as num).toInt(),
        altinSatisFiyati: json['altinSatisFiyati'] == null ? null : (json['altinSatisFiyati'] as num).toDouble(),
      );
}

class PricingCurrent {
  final String code;
  final double finalSatis;
  final DateTime sourceTime;

  PricingCurrent({required this.code, required this.finalSatis, required this.sourceTime});

  factory PricingCurrent.fromJson(Map<String, dynamic> json) => PricingCurrent(
        code: json['code'] as String,
        finalSatis: (json['finalSatis'] as num).toDouble(),
        sourceTime: DateTime.parse(json['sourceTime'] as String),
      );
}

class GoldPrice {
  final double price;
  final DateTime? updatedAt;
  final String? updatedBy;

  GoldPrice({required this.price, this.updatedAt, this.updatedBy});

  factory GoldPrice.fromJson(Map<String, dynamic> json) => GoldPrice(
        price: (json['price'] as num).toDouble(),
        updatedAt: json['updatedAt'] == null ? null : DateTime.parse(json['updatedAt'] as String),
        updatedBy: json['updatedBy'] as String?,
      );
}
