import 'api_client.dart';

class LeaveItem {
  final DateTime from;
  final DateTime to;
  final String? fromTime; // HH:mm
  final String? toTime;   // HH:mm
  final String user;
  final String? reason;
  final String status; // Pending, Approved, Rejected
  LeaveItem({required this.from, required this.to, this.fromTime, this.toTime, required this.user, this.reason, required this.status});

  factory LeaveItem.fromJson(Map<String, dynamic> json) => LeaveItem(
        from: DateTime.parse(json['from'] as String),
        to: DateTime.parse(json['to'] as String),
        fromTime: json['fromTime'] as String?,
        toTime: json['toTime'] as String?,
        user: (json['user'] as String?) ?? 'Bilinmiyor',
        reason: json['reason'] as String?,
        status: (json['status'] as String?) ?? 'Pending',
      );
}

class LeaveService {
  final ApiClient _api;
  LeaveService(this._api);

  Future<List<LeaveItem>> listLeaves({DateTime? from, DateTime? to}) async {
    try {
      final query = <String, dynamic>{};
      if (from != null) {
        final y = from.year.toString().padLeft(4, '0');
        final m = from.month.toString().padLeft(2, '0');
        final d = from.day.toString().padLeft(2, '0');
        query['from'] = '$y-$m-$d';
      }
      if (to != null) {
        final y = to.year.toString().padLeft(4, '0');
        final m = to.month.toString().padLeft(2, '0');
        final d = to.day.toString().padLeft(2, '0');
        query['to'] = '$y-$m-$d';
      }
      final json = await _api.getJson('/api/leaves', query: query.isEmpty ? null : query);
      final items = (json['items'] as List<dynamic>? ?? const [])
          .map((e) => LeaveItem.fromJson(e as Map<String, dynamic>))
          .toList();
      return items;
    } catch (_) {
      return const [];
    }
  }

  Future<void> createLeave(DateTime from, DateTime to, String reason, {String? fromTime, String? toTime}) async {
    String fmt(DateTime d) {
      final y = d.year.toString().padLeft(4, '0');
      final m = d.month.toString().padLeft(2, '0');
      final dd = d.day.toString().padLeft(2, '0');
      return '$y-$m-$dd';
    }
    final body = {
      'from': fmt(from),
      'to': fmt(to),
      'reason': reason,
      if (fromTime != null && toTime != null) 'fromTime': fromTime,
      if (fromTime != null && toTime != null) 'toTime': toTime,
    };
    await _api.postJson('/api/leaves', body);
  }
}
