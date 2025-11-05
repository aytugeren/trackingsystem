import 'api_client.dart';
import 'models.dart';

class InvoiceService {
  final ApiClient _api;
  InvoiceService(this._api);

  Future<CreatedDraftResponse> createDraft(CreateInvoiceDto dto) async {
    final json = await _api.postJson('/api/cashier/invoices/draft', dto.toJson());
    return CreatedDraftResponse.fromJson(json);
  }

  Future<void> finalize(String id) async {
    await _api.postJson('/api/invoices/$id/finalize', const <String, dynamic>{});
  }

  Future<void> setKesildi(String id, bool kesildi) async {
    await _api.putJson('/api/invoices/$id/status', {'kesildi': kesildi});
  }

  Future<void> deleteIfNotKesildi(String id) async {
    await _api.delete('/api/invoices/$id');
  }
}
