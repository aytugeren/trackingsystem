// ignore_for_file: constant_identifier_names
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
  final String? telefon;
  final String? email;

  CreateInvoiceDto({
    required this.tarih,
    this.siraNo = 0,
    required this.musteriAdSoyad,
    required this.tckn,
    required this.tutar,
    required this.odemeSekli,
    required this.altinAyar,
    this.telefon,
    this.email,
  });

  Map<String, dynamic> toJson() {
    final map = <String, dynamic>{
      'tarih': _toDateOnly(tarih),
      'siraNo': siraNo,
      'musteriAdSoyad': musteriAdSoyad,
      'tckn': tckn,
      'tutar': tutar,
      'odemeSekli': odemeSekli.toInt(),
      'altinAyar': altinAyar.toInt(),
    };
    if (telefon != null && telefon!.trim().isNotEmpty) {
      map['telefon'] = telefon;
    }
    if (email != null && email!.trim().isNotEmpty) {
      map['email'] = email;
    }
    return map;
  }
}

class CreateExpenseDto {
  final DateTime tarih; // date only (yyyy-MM-dd)
  final int siraNo; // ignored by draft API; server assigns
  final String musteriAdSoyad;
  final String tckn;
  final double tutar;
  final AltinAyar altinAyar;
  final String? telefon;
  final String? email;

  CreateExpenseDto({
    required this.tarih,
    this.siraNo = 0,
    required this.musteriAdSoyad,
    required this.tckn,
    required this.tutar,
    required this.altinAyar,
    this.telefon,
    this.email,
  });

  Map<String, dynamic> toJson() {
    final map = <String, dynamic>{
      'tarih': _toDateOnly(tarih),
      'siraNo': siraNo,
      'musteriAdSoyad': musteriAdSoyad,
      'tckn': tckn,
      'tutar': tutar,
      'altinAyar': altinAyar.toInt(),
    };
    if (telefon != null && telefon!.trim().isNotEmpty) {
      map['telefon'] = telefon;
    }
    if (email != null && email!.trim().isNotEmpty) {
      map['email'] = email;
    }
    return map;
  }
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

  CreatedDraftResponse(
      {required this.id, required this.siraNo, this.altinSatisFiyati});

  factory CreatedDraftResponse.fromJson(Map<String, dynamic> json) =>
      CreatedDraftResponse(
        id: json['id'] as String,
        siraNo: (json['siraNo'] as num).toInt(),
        altinSatisFiyati: json['altinSatisFiyati'] == null
            ? null
            : (json['altinSatisFiyati'] as num).toDouble(),
      );
}

class PricingCurrent {
  final String code;
  final double finalSatis;
  final DateTime sourceTime;

  PricingCurrent(
      {required this.code, required this.finalSatis, required this.sourceTime});

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
        updatedAt: json['updatedAt'] == null
            ? null
            : DateTime.parse(json['updatedAt'] as String),
        updatedBy: json['updatedBy'] as String?,
      );
}

class GoldFeedHeader {
  final double usdAlis;
  final double usdSatis;
  final double eurAlis;
  final double eurSatis;
  final double eurUsd;
  final double ons;
  final double has;
  final double gumusHas;

  GoldFeedHeader({
    required this.usdAlis,
    required this.usdSatis,
    required this.eurAlis,
    required this.eurSatis,
    required this.eurUsd,
    required this.ons,
    required this.has,
    required this.gumusHas,
  });

  factory GoldFeedHeader.fromJson(Map<String, dynamic> json) => GoldFeedHeader(
        usdAlis: (json['usdAlis'] as num).toDouble(),
        usdSatis: (json['usdSatis'] as num).toDouble(),
        eurAlis: (json['eurAlis'] as num).toDouble(),
        eurSatis: (json['eurSatis'] as num).toDouble(),
        eurUsd: (json['eurUsd'] as num).toDouble(),
        ons: (json['ons'] as num).toDouble(),
        has: (json['has'] as num).toDouble(),
        gumusHas: (json['gumusHas'] as num).toDouble(),
      );
}

class GoldFeedItem {
  final int index;
  final String label;
  final bool isUsed;
  final double? value;

  GoldFeedItem({
    required this.index,
    required this.label,
    required this.isUsed,
    required this.value,
  });

  factory GoldFeedItem.fromJson(Map<String, dynamic> json) {
    final raw = json['value'];
    double? parsed;
    if (raw is num) {
      parsed = raw.toDouble();
    } else if (raw is String) {
      parsed = double.tryParse(raw);
    }
    return GoldFeedItem(
      index: (json['index'] as num).toInt(),
      label: (json['label'] as String?) ?? '',
      isUsed: json['isUsed'] == true,
      value: parsed,
    );
  }
}

class GoldFeedLatest {
  final DateTime fetchedAt;
  final GoldFeedHeader header;
  final List<GoldFeedItem> items;

  GoldFeedLatest({
    required this.fetchedAt,
    required this.header,
    required this.items,
  });

  factory GoldFeedLatest.fromJson(Map<String, dynamic> json) => GoldFeedLatest(
        fetchedAt: DateTime.parse(json['fetchedAt'] as String),
        header: GoldFeedHeader.fromJson(json['header'] as Map<String, dynamic>),
        items: (json['items'] as List? ?? [])
            .whereType<Map>()
            .map((it) =>
                GoldFeedItem.fromJson(it.cast<String, dynamic>()))
            .toList(),
      );
}
