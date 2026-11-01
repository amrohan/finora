import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'inrCurrency',
  standalone: true
})
export class InrCurrencyPipe implements PipeTransform {
  transform(paise: number | bigint | null | undefined, isPrivate: boolean = false): string {
    if (isPrivate) {
      return '••••••';
    }

    if (paise === null || paise === undefined) {
      return '₹0.00';
    }

    const numericPaise = typeof paise === 'bigint' ? Number(paise) : paise;
    const isNegative = numericPaise < 0;
    const absRupees = Math.abs(numericPaise) / 100;

    // Format in Indian numbering system
    const formatted = absRupees.toLocaleString('en-IN', {
      style: 'currency',
      currency: 'INR',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });

    return isNegative ? `-${formatted}` : formatted;
  }
}

