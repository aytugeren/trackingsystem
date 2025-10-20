-- Safe to run multiple times. Uses CONCURRENTLY to minimize write blocking.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Invoices_Kesildi_Tarih_SiraNo" ON "Invoices" ("Kesildi" ASC, "Tarih" DESC, "SiraNo" DESC);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Invoices_KasiyerId" ON "Invoices" ("KasiyerId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Expenses_Kesildi_Tarih_SiraNo" ON "Expenses" ("Kesildi" ASC, "Tarih" DESC, "SiraNo" DESC);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Expenses_KasiyerId" ON "Expenses" ("KasiyerId");
ANALYZE "Invoices";
ANALYZE "Expenses";

