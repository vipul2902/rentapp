import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import type { MoveInInput } from '@/api/tenants';
import { BedPicker } from '@/components/BedPicker';
import { DateField } from '@/components/DateField';
import { TextField } from '@/components/TextField';
import type { VacantBed } from '@/hooks/useTenants';
import { spacing } from '@/theme/tokens';
import { addYears, todayIso } from '@/utils/dates';
import { MONEY_MESSAGE, moneyToInput, parseMoney } from '@/utils/money';

export interface AssignBedForm {
  bedId?: string;
  startDate: string;
  rent: string;
  deposit: string;
  dueDay: string;
  errors: Record<string, string>;
  set: (patch: Partial<Omit<AssignBedForm, 'set' | 'errors' | 'validate' | 'setErrors'>>) => void;
  setErrors: (errors: Record<string, string>) => void;
  /** Returns the request body, or null (and shows errors) if invalid. */
  validate: () => MoveInInput | null;
}

export function useAssignBedForm(initialBedId?: string): AssignBedForm {
  const [state, setState] = useState({ bedId: initialBedId, startDate: todayIso(), rent: '', deposit: '', dueDay: '5' });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = (): MoveInInput | null => {
    const found: Record<string, string> = {};
    const rent = parseMoney(state.rent);
    const deposit = state.deposit.trim() === '0' ? { value: 0 } : parseMoney(state.deposit);
    const dueDay = Number(state.dueDay);
    if (!state.bedId) found.bedId = 'Choose a bed.';
    if (rent.error) found.monthlyRent = rent.error;
    if (!state.rent.trim()) found.monthlyRent = 'Enter the monthly rent.';
    if (deposit.error) found.securityDeposit = MONEY_MESSAGE;
    if (!/^\d{1,2}$/.test(state.dueDay) || dueDay < 1 || dueDay > 31) found.rentDueDay = 'Enter a day between 1 and 31.';
    setErrors(found);
    if (Object.keys(found).length > 0 || !state.bedId) return null;
    return {
      bedId: state.bedId,
      startDate: state.startDate,
      monthlyRent: rent.value,
      securityDeposit: deposit.value ?? 0,
      rentDueDay: dueDay,
    };
  };

  return { ...state, errors, set: (patch) => setState((s) => ({ ...s, ...patch })), setErrors, validate };
}

/** Bed, move-in date, rent, deposit and due day — shared by "Add tenant" and "Assign bed". */
export function AssignBedFields({ form, propertyId }: { form: AssignBedForm; propertyId?: string }) {
  const today = todayIso();
  const chooseBed = (bed: VacantBed) =>
    form.set({ bedId: bed.bedId, rent: form.rent || moneyToInput(bed.defaultMonthlyRent) });

  return (
    <View style={styles.container}>
      <BedPicker value={form.bedId} onChange={chooseBed} propertyId={propertyId} error={form.errors.bedId} />
      <DateField
        label="Move-in date"
        value={form.startDate}
        onChange={(startDate) => form.set({ startDate })}
        minimumDate={addYears(today, -10)}
        maximumDate={addYears(today, 1)}
        error={form.errors.startDate}
        hint="Past dates are fine for tenants who already live here. A future date holds the bed as reserved."
      />
      <TextField
        label="Monthly rent"
        value={form.rent}
        onChangeText={(rent) => form.set({ rent })}
        error={form.errors.monthlyRent}
        keyboardType="decimal-pad"
        placeholder="e.g. 8500"
      />
      <TextField
        label="Security deposit"
        value={form.deposit}
        onChangeText={(deposit) => form.set({ deposit })}
        error={form.errors.securityDeposit}
        keyboardType="decimal-pad"
        placeholder="0"
      />
      <TextField
        label="Rent due on day"
        value={form.dueDay}
        onChangeText={(dueDay) => form.set({ dueDay })}
        error={form.errors.rentDueDay}
        keyboardType="number-pad"
        hint="Day of each month (1–31). Shorter months use their last day."
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.lg },
});
