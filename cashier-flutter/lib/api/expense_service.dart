import 'api_client.dart';
import 'models.dart';

class ExpenseService {
  final ApiClient _api;
  ExpenseService(this._api);

  Future<CreatedDraftResponse> createDraft(CreateExpenseDto dto) async {
    final json = await _api.postJson('/api/cashier/expenses/draft', dto.toJson());
    return CreatedDraftResponse.fromJson(json);
  }

  Future<void> finalize(String id) async {
    await _api.postJson('/api/expenses/$id/finalize', const <String, dynamic>{});
  }

  Future<void> setKesildi(String id, bool kesildi) async {
    await _api.putJson('/api/expenses/$id/status', {'kesildi': kesildi});
  }

  Future<void> deleteIfNotKesildi(String id) async {
    await _api.delete('/api/expenses/$id');
  }
}
