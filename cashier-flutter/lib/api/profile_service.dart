import 'api_client.dart';

class ProfileMe {
  final String? id;
  final String? email;
  final String? role;
  final int allowanceDays;
  final double usedDays;
  final double remainingDays;

  ProfileMe({
    required this.id,
    required this.email,
    required this.role,
    required this.allowanceDays,
    required this.usedDays,
    required this.remainingDays,
  });

  factory ProfileMe.fromJson(Map<String, dynamic> json) => ProfileMe(
        id: json['id']?.toString(),
        email: json['email'] as String?,
        role: json['role'] as String?,
        allowanceDays: (json['allowanceDays'] as num?)?.toInt() ?? 14,
        usedDays: (json['usedDays'] as num?)?.toDouble() ?? 0.0,
        remainingDays: (json['remainingDays'] as num?)?.toDouble() ?? 0.0,
      );
}

class ProfileService {
  final ApiClient _api;
  ProfileService(this._api);

  Future<ProfileMe?> getMe() async {
    try {
      final json = await _api.getJson('/api/me');
      return ProfileMe.fromJson(json);
    } catch (_) {
      return null;
    }
  }
}

