import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { InrCurrencyPipe } from '../../shared/pipes/inr-currency.pipe';
import { HouseholdService } from '../../core/services/household.service';
import { AccountService } from '../../core/services/account.service';
import { TransactionService, Transaction } from '../../core/services/transaction.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzCardModule,
    NzStatisticModule,
    NzButtonModule,
    NzEmptyModule,
    NzTagModule,
    InrCurrencyPipe
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto">
      <!-- Welcome Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Household Dashboard</h1>
          <p class="text-sm text-slate-500 mt-1">Overview of your family's collective and personal finances</p>
        </div>
        <div class="flex items-center gap-2">
          <a routerLink="/transactions" nz-button nzType="default" class="rounded-lg text-sm font-medium">
            <span>🔄 Record Transfer</span>
          </a>
          <a routerLink="/transactions" nz-button nzType="primary" class="rounded-lg text-sm font-medium bg-emerald-600 hover:bg-emerald-500 border-0">
            <span>➕ Add Transaction</span>
          </a>
        </div>
      </div>

      <!-- Financial Stat Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- Net Worth -->
        <nz-card class="rounded-xl border border-slate-200/80 shadow-xs hover:shadow-sm transition-shadow">
          <div class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Household Net Worth</div>
          <div class="text-2xl font-bold font-mono text-slate-900 tracking-tight">
            {{ accountService.totalNetWorth() | inrCurrency }}
          </div>
          <div class="text-xs text-slate-400 font-medium mt-2 flex items-center gap-1">
            <span>Across {{ accountService.accounts().length }} active accounts</span>
          </div>
        </nz-card>

        <!-- Income -->
        <nz-card class="rounded-xl border border-slate-200/80 shadow-xs hover:shadow-sm transition-shadow">
          <div class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Monthly Inflow</div>
          <div class="text-2xl font-bold font-mono text-emerald-600 tracking-tight">
            {{ monthlyIncomePaise() | inrCurrency }}
          </div>
          <div class="text-xs text-slate-400 font-medium mt-2">Earned income this month</div>
        </nz-card>

        <!-- Expenses -->
        <nz-card class="rounded-xl border border-slate-200/80 shadow-xs hover:shadow-sm transition-shadow">
          <div class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Monthly Outflow</div>
          <div class="text-2xl font-bold font-mono text-rose-600 tracking-tight">
            {{ monthlyExpensePaise() | inrCurrency }}
          </div>
          <div class="text-xs text-slate-400 font-medium mt-2">Excludes account transfers</div>
        </nz-card>

        <!-- Savings Rate -->
        <nz-card class="rounded-xl border border-slate-200/80 shadow-xs hover:shadow-sm transition-shadow">
          <div class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Net Cashflow</div>
          <div
            class="text-2xl font-bold font-mono tracking-tight"
            [ngClass]="netCashflowPaise() >= 0 ? 'text-emerald-600' : 'text-rose-600'">
            {{ netCashflowPaise() | inrCurrency }}
          </div>
          <div class="text-xs text-emerald-600 font-medium mt-2">Inflow minus Outflow</div>
        </nz-card>
      </div>

      <!-- Main Dashboard Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Recent Transactions -->
        <div class="lg:col-span-2">
          <nz-card [nzTitle]="'Recent Activity'" [nzExtra]="viewAllTpl" class="rounded-xl border border-slate-200/80 shadow-xs">
            <ng-template #viewAllTpl>
              <a routerLink="/transactions" class="text-xs font-medium text-emerald-600 hover:text-emerald-500">View All →</a>
            </ng-template>

            @if (recentTransactions().length === 0) {
              <nz-empty
                nzNotFoundImage="simple"
                nzNotFoundContent="No transactions recorded yet in this household.">
                <a routerLink="/transactions" nz-button nzType="primary" class="mt-2 bg-emerald-600 border-0 rounded-lg text-sm">
                  Add First Transaction
                </a>
              </nz-empty>
            } @else {
              <div class="divide-y divide-slate-100">
                @for (txn of recentTransactions(); track txn.id) {
                  <div class="py-3 flex items-center justify-between gap-4">
                    <div class="flex items-center gap-3">
                      <div class="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center text-sm">
                        {{ txn.transactionType === 1 ? '💸' : (txn.transactionType === 2 ? '💰' : '🔄') }}
                      </div>
                      <div>
                        <div class="font-medium text-slate-900 text-sm">
                          {{ txn.payee || (txn.transactionType === 3 ? 'Account Transfer' : 'Direct Transaction') }}
                        </div>
                        <div class="text-xs text-slate-400 font-mono">{{ txn.date }} • {{ txn.accountName }}</div>
                      </div>
                    </div>
                    <div class="text-right">
                      <div
                        class="font-mono font-bold text-sm tracking-tight"
                        [ngClass]="{
                          'text-rose-600': txn.transactionType === 1,
                          'text-emerald-600': txn.transactionType === 2,
                          'text-blue-600': txn.transactionType === 3
                        }">
                        {{ txn.transactionType === 1 ? '-' : (txn.transactionType === 2 ? '+' : '') }}
                        {{ txn.amountPaise | inrCurrency }}
                      </div>
                      @if (txn.categoryName) {
                        <span class="text-[11px] text-slate-400">{{ txn.categoryName }}</span>
                      }
                    </div>
                  </div>
                }
              </div>
            }
          </nz-card>
        </div>

        <!-- Accounts Overview in Sidebar -->
        <div class="space-y-6">
          <nz-card [nzTitle]="'Your Accounts'" [nzExtra]="manageAccTpl" class="rounded-xl border border-slate-200/80 shadow-xs">
            <ng-template #manageAccTpl>
              <a routerLink="/accounts" class="text-xs font-medium text-emerald-600 hover:text-emerald-500">Manage →</a>
            </ng-template>

            @if (accountService.accounts().length === 0) {
              <nz-empty nzNotFoundImage="simple" nzNotFoundContent="No accounts added yet.">
                <a routerLink="/accounts" nz-button nzType="primary" class="mt-2 bg-emerald-600 border-0 rounded-lg text-sm">
                  Add Account
                </a>
              </nz-empty>
            } @else {
              <div class="space-y-3">
                @for (acc of accountService.accounts(); track acc.id) {
                  <div class="flex items-center justify-between p-2.5 rounded-lg bg-slate-50/70 border border-slate-100">
                    <div>
                      <span class="font-medium text-slate-800 text-sm block">{{ acc.name }}</span>
                      <span class="text-xs text-slate-400">{{ acc.accountNumberMask || 'Active' }}</span>
                    </div>
                    <span class="font-mono font-bold text-slate-900 text-sm">
                      {{ acc.currentBalancePaise | inrCurrency }}
                    </span>
                  </div>
                }
              </div>
            }
          </nz-card>
        </div>
      </div>
    </div>
  `
})
export class DashboardComponent implements OnInit {
  householdService = inject(HouseholdService);
  accountService = inject(AccountService);
  transactionService = inject(TransactionService);

  recentTransactions = signal<Transaction[]>([]);

  monthlyIncomePaise = computed(() => {
    return this.transactionService.transactions()
      .filter(t => t.transactionType === 2) // Income
      .reduce((sum, t) => sum + Number(t.amountPaise), 0);
  });

  monthlyExpensePaise = computed(() => {
    return this.transactionService.transactions()
      .filter(t => t.transactionType === 1) // Expense (excludes Transfer = 3)
      .reduce((sum, t) => sum + Number(t.amountPaise), 0);
  });

  netCashflowPaise = computed(() => {
    return this.monthlyIncomePaise() - this.monthlyExpensePaise();
  });

  ngOnInit(): void {
    this.accountService.loadAccounts().subscribe();
    this.transactionService.loadTransactions({ pageSize: 5 }).subscribe(res => {
      this.recentTransactions.set(res.items);
    });
  }
}
