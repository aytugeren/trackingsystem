import 'api_client.dart';
import 'models.dart';

class PricingService {
  final ApiClient _api;
  PricingService(this._api);

  Future<PricingCurrent?> current() async {
    try {
      final json = await _api.getJson('/api/pricing/current');
      return PricingCurrent.fromJson(json);
    } catch (_) {
      return null;
    }
  }
}

