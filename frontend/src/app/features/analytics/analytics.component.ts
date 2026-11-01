import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { AnalyticsService, NetWorthOverview, SpendingBreakdown } from '../../core/services/analytics.service';
import { InrCurrencyPipe } from '../../shared/pipes/inr-currency.pipe';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzSelectModule,
    NzProgressModule,
    NzTableModule,
    NzTagModule,
    NzSpinModule,
    NzDropdownModule,
    InrCurrencyPipe
  ],
  template: `
    <div class="max-w-7xl mx-auto space-y-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold text-slate-900 tracking-tight flex items-center gap-2">
            <span>📈</span>
            <span>Analytics & Financial Insights</span>
          </h1>
          <p class="text-sm text-slate-500 mt-1">
            Privacy-safe Net Worth computation, multi-month cash flows, and category distributions.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <!-- Export Transactions Dropdown -->
          <button
            nz-button
            nzType="default"
            nz-dropdown
            [nzDropdownMenu]="txExportMenu"
            class="flex items-center gap-2 rounded-xl text-slate-700 shadow-xs border-slate-200">
            <span>🧾</span>
            <span>Export Transactions</span>
          </button>
          <nz-dropdown-menu #txExportMenu="nzDropdownMenu">
            <ul nz-menu class="min-w-36 rounded-xl shadow-lg border border-slate-100 p-1">
              <li nz-menu-item (click)="downloadTransactions('csv')">Download as CSV</li>
              <li nz-menu-item (click)="downloadTransactions('json')">Download as JSON</li>
            </ul>
          </nz-dropdown-menu>

          <!-- Export Accounts Dropdown -->
          <button
            nz-button
            nzType="default"
            nz-dropdown
            [nzDropdownMenu]="accExportMenu"
            class="flex items-center gap-2 rounded-xl text-slate-700 shadow-xs border-slate-200">
            <span>🏦</span>
            <span>Export Accounts</span>
          </button>
          <nz-dropdown-menu #accExportMenu="nzDropdownMenu">
            <ul nz-menu class="min-w-36 rounded-xl shadow-lg border border-slate-100 p-1">
              <li nz-menu-item (click)="downloadAccounts('csv')">Download as CSV</li>
              <li nz-menu-item (click)="downloadAccounts('json')">Download as JSON</li>
            </ul>
          </nz-dropdown-menu>
        </div>
      </div>

      <!-- Loading State -->
      @if (loadingOverview()) {
        <div class="flex justify-center items-center py-24">
          <nz-spin nzSimple nzSize="large"></nz-spin>
        </div>
      } @else if (overview()) {
        <!-- Net Worth & Cash Flow Summary Cards -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-5">
          <!-- Net Worth Card -->
          <div class="bg-gradient-to-br from-slate-900 to-slate-950 p-5 rounded-2xl text-white shadow-md relative overflow-hidden">
            <div class="absolute -right-4 -bottom-4 w-28 h-28 bg-emerald-500/10 rounded-full blur-xl pointer-events-none"></div>
            <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold block">Total Net Worth</span>
            <div class="text-3xl font-bold tracking-tight text-emerald-400 mt-2">
              {{ overview()!.netWorthPaise | inrCurrency }}
            </div>
            <div class="flex items-center justify-between text-xs text-slate-400 mt-4 pt-3 border-t border-slate-800">
              <span>Assets: <strong class="text-white">{{ overview()!.totalAssetsPaise | inrCurrency }}</strong></span>
              <span>Debt: <strong class="text-rose-400">{{ overview()!.totalLiabilitiesPaise | inrCurrency }}</strong></span>
            </div>
          </div>

          <!-- Total Assets Card -->
          <div class="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
            <div class="flex items-center justify-between">
              <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold">Total Assets</span>
              <span class="text-xl">💰</span>
            </div>
            <div class="text-2xl font-bold text-slate-900 mt-2">
              {{ overview()!.totalAssetsPaise | inrCurrency }}
            </div>
            <div class="text-xs text-slate-500 mt-3 flex items-center gap-1">
              <span>Liquid savings, bank accounts & investments</span>
            </div>
          </div>

          <!-- Total Liabilities Card -->
          <div class="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
            <div class="flex items-center justify-between">
              <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold">Total Liabilities</span>
              <span class="text-xl">💳</span>
            </div>
            <div class="text-2xl font-bold text-rose-600 mt-2">
              {{ overview()!.totalLiabilitiesPaise | inrCurrency }}
            </div>
            <div class="text-xs text-slate-500 mt-3 flex items-center gap-1">
              <span>Outstanding loans & credit card balances</span>
            </div>
          </div>

          <!-- Savings Rate Card -->
          <div class="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
            <div class="flex items-center justify-between">
              <span class="text-xs uppercase tracking-wider text-slate-400 font-semibold">Monthly Savings Rate</span>
              <span class="text-xl">🎯</span>
            </div>
            <div class="text-2xl font-bold text-slate-900 mt-2 flex items-center gap-2">
              <span>{{ overview()!.savingsRatePercentage }}%</span>
              <nz-tag [nzColor]="overview()!.savingsRatePercentage >= 20 ? 'success' : overview()!.savingsRatePercentage > 0 ? 'warning' : 'error'" class="rounded-full font-medium">
                {{ overview()!.savingsRatePercentage >= 20 ? 'Optimal' : overview()!.savingsRatePercentage > 0 ? 'Positive' : 'Deficit' }}
              </nz-tag>
            </div>
            <div class="text-xs text-slate-500 mt-3 flex items-center justify-between">
              <span>Inflow: {{ overview()!.currentMonthInflowPaise | inrCurrency }}</span>
              <span>Outflow: {{ overview()!.currentMonthOutflowPaise | inrCurrency }}</span>
            </div>
          </div>
        </div>

        <!-- Cash Flow History Trend (Multi-Month) -->
        <div class="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-lg font-bold text-slate-900">Cash Flow History (Last 6 Months)</h2>
              <p class="text-xs text-slate-500">Transfers are strictly excluded from monthly income and expense calculations.</p>
            </div>
            <div class="flex items-center gap-4 text-xs">
              <div class="flex items-center gap-1.5">
                <span class="w-3 h-3 rounded-full bg-emerald-500 inline-block"></span>
                <span class="text-slate-600 font-medium">Inflow (Income)</span>
              </div>
              <div class="flex items-center gap-1.5">
                <span class="w-3 h-3 rounded-full bg-rose-500 inline-block"></span>
                <span class="text-slate-600 font-medium">Outflow (Expense)</span>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 pt-2">
            @for (cf of overview()!.cashFlowHistory; track cf.year + '-' + cf.month) {
              <div class="bg-slate-50 p-4 rounded-xl border border-slate-100 flex flex-col justify-between space-y-3">
                <div class="text-center pb-2 border-b border-slate-200">
                  <span class="text-xs font-bold text-slate-700">{{ getMonthName(cf.month) }} {{ cf.year }}</span>
                </div>
                <div class="space-y-2 text-xs">
                  <div class="flex items-center justify-between text-emerald-700">
                    <span>In:</span>
                    <span class="font-semibold">{{ cf.inflowPaise | inrCurrency }}</span>
                  </div>
                  <div class="flex items-center justify-between text-rose-700">
                    <span>Out:</span>
                    <span class="font-semibold">{{ cf.outflowPaise | inrCurrency }}</span>
                  </div>
                  <div class="pt-2 border-t border-slate-200 flex items-center justify-between font-bold"
                       [class.text-emerald-700]="cf.netCashFlowPaise >= 0"
                       [class.text-rose-700]="cf.netCashFlowPaise < 0">
                    <span>Net:</span>
                    <span>{{ cf.netCashFlowPaise | inrCurrency }}</span>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Spending Breakdown Section -->
        <div class="space-y-4">
          <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
            <div>
              <h2 class="text-lg font-bold text-slate-900">Spending Breakdown</h2>
              <p class="text-xs text-slate-500">Categorical and merchant allocations for the active billing cycle.</p>
            </div>
            
            <div class="flex items-center gap-2">
              <nz-select [(ngModel)]="selectedMonth" (ngModelChange)="loadSpendingBreakdown()" class="w-36">
                @for (m of monthsList; track m.value) {
                  <nz-option [nzValue]="m.value" [nzLabel]="m.label"></nz-option>
                }
              </nz-select>

              <nz-select [(ngModel)]="selectedYear" (ngModelChange)="loadSpendingBreakdown()" class="w-28">
                @for (y of yearsList; track y) {
                  <nz-option [nzValue]="y" [nzLabel]="y.toString()"></nz-option>
                }
              </nz-select>
            </div>
          </div>

          @if (loadingBreakdown()) {
            <div class="flex justify-center py-12">
              <nz-spin nzSimple></nz-spin>
            </div>
          } @else if (breakdown()) {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <!-- Categories Distribution (2 cols) -->
              <div class="lg:col-span-2 bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-bold uppercase tracking-wider text-slate-600">Categories (Total: {{ breakdown()!.totalExpensePaise | inrCurrency }})</h3>
                  <span class="text-xs text-slate-400">{{ breakdown()!.categories.length }} Active Categories</span>
                </div>

                @if (breakdown()!.categories.length === 0) {
                  <div class="py-12 text-center text-slate-400 text-sm">
                    No expense transactions recorded for this month.
                  </div>
                } @else {
                  <div class="space-y-4">
                    @for (cat of breakdown()!.categories; track cat.categoryName) {
                      <div class="space-y-1.5">
                        <div class="flex items-center justify-between text-sm">
                          <div class="flex items-center gap-2">
                            <span class="text-base">{{ cat.icon || '🏷️' }}</span>
                            <span class="font-medium text-slate-800">{{ cat.categoryName }}</span>
                          </div>
                          <div class="flex items-center gap-3">
                            <span class="font-bold text-slate-900">{{ cat.amountPaise | inrCurrency }}</span>
                            <span class="text-xs text-slate-500 w-12 text-right">({{ cat.percentage }}%)</span>
                          </div>
                        </div>
                        <nz-progress
                          [nzPercent]="cat.percentage"
                          [nzShowInfo]="false"
                          [nzStrokeColor]="cat.colorHex || '#10b981'"
                          nzSize="small">
                        </nz-progress>
                      </div>
                    }
                  </div>
                }
              </div>

              <!-- Top Payees / Merchants (1 col) -->
              <div class="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-bold uppercase tracking-wider text-slate-600">Top Payees</h3>
                  <span class="text-xs text-slate-400">Top 10</span>
                </div>

                @if (breakdown()!.topPayees.length === 0) {
                  <div class="py-12 text-center text-slate-400 text-sm">
                    No payee data recorded.
                  </div>
                } @else {
                  <div class="divide-y divide-slate-100">
                    @for (p of breakdown()!.topPayees; track p.payee) {
                      <div class="py-2.5 flex items-center justify-between text-sm first:pt-0 last:pb-0">
                        <div class="overflow-hidden pr-2">
                          <span class="font-medium text-slate-800 block truncate">{{ p.payee }}</span>
                          <span class="text-[11px] text-slate-400 block">{{ p.transactionCount }} transactions</span>
                        </div>
                        <span class="font-bold text-slate-900 whitespace-nowrap">{{ p.amountPaise | inrCurrency }}</span>
                      </div>
                    }
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class AnalyticsComponent implements OnInit {
  private analyticsService = inject(AnalyticsService);
  private message = inject(NzMessageService);

  loadingOverview = signal(true);
  loadingBreakdown = signal(false);

  overview = signal<NetWorthOverview | null>(null);
  breakdown = signal<SpendingBreakdown | null>(null);

  selectedMonth = new Date().getMonth() + 1;
  selectedYear = new Date().getFullYear();

  monthsList = [
    { value: 1, label: 'January' },
    { value: 2, label: 'February' },
    { value: 3, label: 'March' },
    { value: 4, label: 'April' },
    { value: 5, label: 'May' },
    { value: 6, label: 'June' },
    { value: 7, label: 'July' },
    { value: 8, label: 'August' },
    { value: 9, label: 'September' },
    { value: 10, label: 'October' },
    { value: 11, label: 'November' },
    { value: 12, label: 'December' }
  ];

  yearsList = [2025, 2026, 2027];

  ngOnInit(): void {
    this.loadOverview();
    this.loadSpendingBreakdown();
  }

  loadOverview(): void {
    this.loadingOverview.set(true);
    this.analyticsService.getOverview(6).subscribe({
      next: (data) => {
        this.overview.set(data);
        this.loadingOverview.set(false);
      },
      error: () => {
        this.message.error('Failed to load financial overview.');
        this.loadingOverview.set(false);
      }
    });
  }

  loadSpendingBreakdown(): void {
    this.loadingBreakdown.set(true);
    this.analyticsService.getSpendingBreakdown(this.selectedMonth, this.selectedYear).subscribe({
      next: (data) => {
        this.breakdown.set(data);
        this.loadingBreakdown.set(false);
      },
      error: () => {
        this.message.error('Failed to load spending breakdown.');
        this.loadingBreakdown.set(false);
      }
    });
  }

  downloadTransactions(format: 'csv' | 'json'): void {
    this.analyticsService.exportTransactions(format).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `transactions-${new Date().toISOString().split('T')[0]}.${format}`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.message.success(`Transactions exported as ${format.toUpperCase()}`);
      },
      error: () => {
        this.message.error('Failed to export transactions.');
      }
    });
  }

  downloadAccounts(format: 'csv' | 'json'): void {
    this.analyticsService.exportAccounts(format).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `accounts-${new Date().toISOString().split('T')[0]}.${format}`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.message.success(`Accounts exported as ${format.toUpperCase()}`);
      },
      error: () => {
        this.message.error('Failed to export accounts.');
      }
    });
  }

  getMonthName(monthNum: number): string {
    const found = this.monthsList.find(m => m.value === monthNum);
    return found ? found.label.slice(0, 3) : '';
  }
}

