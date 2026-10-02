namespace RentApp.Infrastructure.Persistence.Migrations;

/// <summary>
/// Database triggers that make financial and audit history permanent, whatever code or person is connected:
/// - audit entries, waivers, payment allocations and receipts can never be changed or deleted;
/// - payments and rent charges can never be deleted;
/// - a payment can only change by being voided once (who, when, why); its amount, tenant, date and
///   method are fixed.
/// Kept in its own file so later migrations can re-create the guards if a table is rebuilt.
/// </summary>
internal static class FinancialRecordGuards
{
    public const string Create = """
        CREATE FUNCTION rentapp_reject_change() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION '% on % is not allowed: financial and audit records are permanent', TG_OP, TG_TABLE_NAME
                USING ERRCODE = 'restrict_violation';
        END;
        $$;

        CREATE FUNCTION rentapp_guard_payment_update() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF OLD.status = 'Voided'
               OR NEW.status <> 'Voided'
               OR NEW.id <> OLD.id
               OR NEW.organization_id <> OLD.organization_id
               OR NEW.tenant_id <> OLD.tenant_id
               OR NEW.amount <> OLD.amount
               OR NEW.payment_date <> OLD.payment_date
               OR NEW.method <> OLD.method
               OR NEW.created_at <> OLD.created_at
               OR NEW.idempotency_key IS DISTINCT FROM OLD.idempotency_key THEN
                RAISE EXCEPTION 'A payment can only be changed by voiding it once' USING ERRCODE = 'restrict_violation';
            END IF;
            RETURN NEW;
        END;
        $$;

        CREATE TRIGGER trg_audit_logs_permanent BEFORE UPDATE OR DELETE ON audit_logs
            FOR EACH ROW EXECUTE FUNCTION rentapp_reject_change();
        CREATE TRIGGER trg_rent_charge_adjustments_permanent BEFORE UPDATE OR DELETE ON rent_charge_adjustments
            FOR EACH ROW EXECUTE FUNCTION rentapp_reject_change();
        CREATE TRIGGER trg_payment_allocations_permanent BEFORE UPDATE OR DELETE ON payment_allocations
            FOR EACH ROW EXECUTE FUNCTION rentapp_reject_change();
        CREATE TRIGGER trg_receipts_permanent BEFORE UPDATE OR DELETE ON receipts
            FOR EACH ROW EXECUTE FUNCTION rentapp_reject_change();
        CREATE TRIGGER trg_payments_no_delete BEFORE DELETE ON payments
            FOR EACH ROW EXECUTE FUNCTION rentapp_reject_change();
        CREATE TRIGGER trg_rent_charges_no_delete BEFORE DELETE ON rent_charges
            FOR EACH ROW EXECUTE FUNCTION rentapp_reject_change();
        CREATE TRIGGER trg_payments_void_only BEFORE UPDATE ON payments
            FOR EACH ROW EXECUTE FUNCTION rentapp_guard_payment_update();
        """;

    public const string Drop = """
        DROP TRIGGER IF EXISTS trg_payments_void_only ON payments;
        DROP TRIGGER IF EXISTS trg_rent_charges_no_delete ON rent_charges;
        DROP TRIGGER IF EXISTS trg_payments_no_delete ON payments;
        DROP TRIGGER IF EXISTS trg_receipts_permanent ON receipts;
        DROP TRIGGER IF EXISTS trg_payment_allocations_permanent ON payment_allocations;
        DROP TRIGGER IF EXISTS trg_rent_charge_adjustments_permanent ON rent_charge_adjustments;
        DROP TRIGGER IF EXISTS trg_audit_logs_permanent ON audit_logs;
        DROP FUNCTION IF EXISTS rentapp_guard_payment_update();
        DROP FUNCTION IF EXISTS rentapp_reject_change();
        """;
}
