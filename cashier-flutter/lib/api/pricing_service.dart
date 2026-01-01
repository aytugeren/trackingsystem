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

  Future<GoldPrice?> gold() async {
    try {
      final json = await _api.getJson('/api/pricing/gold');
      return GoldPrice.fromJson(json);
    } catch (_) {
      return null;
    }
  }

  Future<GoldPrice> updateGold(double price) async {
    final json = await _api.putJson('/api/pricing/gold', {'price': price});
    // PUT returns the latest price payload
    return GoldPrice.fromJson(json);
  }

  Future<GoldFeedLatest?> feedLatest() async {
    try {
      final json = await _api.getJson('/api/pricing/feed/latest');
      return GoldFeedLatest.fromJson(json);
    } catch (_) {
      return null;
    }
  }
}
