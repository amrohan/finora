import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { InrCurrencyPipe } from '../../shared/pipes/inr-currency.pipe';
import { HouseholdService } from '../../core/services/household.service';
import { AccountService } from '../../core/services/account.service';
import { TransactionService, Transaction } from '../../core/services/transaction.service';
import { BillService, BillOccurrence } from '../../core/services/bill.service';

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
    NzIconModule,
    InrCurrencyPipe
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto">
      <!-- Welcome Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Dashboard</h1>
          <p class="text-xs sm:text-sm text-slate-500 mt-0.5">Overview of family and personal finances</p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <a routerLink="/analytics" nz-button nzType="default" class="rounded-xl text-xs sm:text-sm font-medium border-slate-200 text-slate-700 bg-white hover:bg-slate-50 shadow-2xs flex items-center gap-1.5">
            <span nz-icon nzType="rise"></span>
            <span>Analytics</span>
          </a>
          <a routerLink="/transactions" nz-button nzType="default" class="rounded-xl text-xs sm:text-sm font-medium border-slate-200 text-slate-700 bg-white hover:bg-slate-50 shadow-2xs flex items-center gap-1.5">
            <span nz-icon nzType="swap"></span>
            <span>Transfer</span>
          </a>
          <a routerLink="/transactions" nz-button nzType="primary" class="rounded-xl text-xs sm:text-sm font-medium bg-emerald-600 hover:bg-emerald-500 border-0 shadow-2xs flex items-center gap-1.5">
            <span nz-icon nzType="plus"></span>
            <span>Add Transaction</span>
          </a>
        </div>
      </div>

      <!-- Financial Stat Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- Net Worth -->
        <nz-card class="rounded-2xl border border-slate-200/80 shadow-2xs hover:shadow-xs transition-shadow">
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold text-slate-400 uppercase tracking-wider">Net Worth</span>
            <div class="w-8 h-8 rounded-xl bg-slate-100 text-slate-700 flex items-center justify-center text-sm">
              <span nz-icon nzType="bank"></span>
            </div>
          </div>
          <div class="text-2xl font-bold text-slate-900 tracking-tight mt-2">
            {{ accountService.totalNetWorth() | inrCurrency }}
          </div>
          <div class="text-xs text-slate-400 font-medium mt-2 flex items-center gap-1">
            <span>{{ accountService.accounts().length }} active accounts</span>
          </div>
        </nz-card>

        <!-- Income -->
        <nz-card class="rounded-2xl border border-slate-200/80 shadow-2xs hover:shadow-xs transition-shadow">
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold text-slate-400 uppercase tracking-wider">Monthly Inflow</span>
            <div class="w-8 h-8 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center text-sm">
              <span nz-icon nzType="arrow-up"></span>
            </div>
          </div>
          <div class="text-2xl font-bold text-emerald-600 tracking-tight mt-2">
            {{ monthlyIncomePaise() | inrCurrency }}
          </div>
          <div class="text-xs text-slate-400 font-medium mt-2">Earned income this month</div>
        </nz-card>

        <!-- Expenses -->
        <nz-card class="rounded-2xl border border-slate-200/80 shadow-2xs hover:shadow-xs transition-shadow">
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold text-slate-400 uppercase tracking-wider">Monthly Outflow</span>
            <div class="w-8 h-8 rounded-xl bg-rose-50 text-rose-600 flex items-center justify-center text-sm">
              <span nz-icon nzType="arrow-down"></span>
            </div>
          </div>
          <div class="text-2xl font-bold text-rose-600 tracking-tight mt-2">
            {{ monthlyExpensePaise() | inrCurrency }}
          </div>
          <div class="text-xs text-slate-400 font-medium mt-2">Excludes account transfers</div>
        </nz-card>

        <!-- Net Cashflow -->
        <nz-card class="rounded-2xl border border-slate-200/80 shadow-2xs hover:shadow-xs transition-shadow">
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold text-slate-400 uppercase tracking-wider">Net Cashflow</span>
            <div class="w-8 h-8 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center text-sm">
              <span nz-icon nzType="wallet"></span>
            </div>
          </div>
          <div
            class="text-2xl font-bold tracking-tight mt-2"
            [ngClass]="netCashflowPaise() >= 0 ? 'text-emerald-600' : 'text-rose-600'">
            {{ netCashflowPaise() | inrCurrency }}
          </div>
          <div class="text-xs text-slate-400 font-medium mt-2">Inflow minus Outflow</div>
        </nz-card>
      </div>

      <!-- Main Dashboard Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Recent Transactions (2 cols on lg) -->
        <div class="lg:col-span-2 space-y-6">
          <nz-card [nzTitle]="'Recent Activity'" [nzExtra]="viewAllTpl" class="rounded-2xl border border-slate-200/80 shadow-2xs">
            <ng-template #viewAllTpl>
              <a routerLink="/transactions" class="text-xs font-semibold text-emerald-600 hover:text-emerald-500">View All →</a>
            </ng-template>

            @if (recentTransactions().length === 0) {
              <nz-empty
                nzNotFoundImage="simple"
                nzNotFoundContent="No transactions recorded yet in this household.">
                <a routerLink="/transactions" nz-button nzType="primary" class="mt-2 bg-emerald-600 border-0 rounded-xl text-sm">
                  Add First Transaction
                </a>
              </nz-empty>
            } @else {
              <div class="divide-y divide-slate-100">
                @for (txn of recentTransactions(); track txn.id) {
                  <div class="py-3.5 flex items-center justify-between gap-4 first:pt-0 last:pb-0">
                    <div class="flex items-center gap-3">
                      <!-- Directional Category Icon -->
                      <div class="w-9 h-9 rounded-xl flex items-center justify-center text-sm flex-shrink-0"
                           [ngClass]="{
                             'bg-rose-50 text-rose-600': txn.transactionType === 1,
                             'bg-emerald-50 text-emerald-600': txn.transactionType === 2,
                             'bg-blue-50 text-blue-600': txn.transactionType === 3
                           }">
                        <span nz-icon [nzType]="txn.transactionType === 1 ? 'arrow-down' : (txn.transactionType === 2 ? 'arrow-up' : 'swap')"></span>
                      </div>
                      <div class="overflow-hidden">
                        <div class="font-semibold text-slate-800 text-sm truncate">
                          {{ txn.payee || (txn.transactionType === 3 ? 'Account Transfer' : 'Direct Transaction') }}
                        </div>
                        <div class="text-xs text-slate-400 font-mono">{{ txn.date }} • {{ txn.accountName }}</div>
                      </div>
                    </div>
                    <div class="text-right flex-shrink-0">
                      <div
                        class="font-bold text-sm tracking-tight"
                        [ngClass]="{
                          'text-rose-600': txn.transactionType === 1,
                          'text-emerald-600': txn.transactionType === 2,
                          'text-blue-600': txn.transactionType === 3
                        }">
                        {{ txn.transactionType === 1 ? '-' : (txn.transactionType === 2 ? '+' : '') }}
                        {{ txn.amountPaise | inrCurrency }}
                      </div>
                      @if (txn.categoryName) {
                        <span class="text-[11px] text-slate-400 block">{{ txn.categoryName }}</span>
                      }
                    </div>
                  </div>
                }
              </div>
            }
          </nz-card>
        </div>

        <!-- Sidebar Column (1 col on lg) -->
        <div class="space-y-6">
          <!-- Accounts Card -->
          <nz-card [nzTitle]="'Your Accounts'" [nzExtra]="manageAccTpl" class="rounded-2xl border border-slate-200/80 shadow-2xs">
            <ng-template #manageAccTpl>
              <a routerLink="/accounts" class="text-xs font-semibold text-emerald-600 hover:text-emerald-500">Manage →</a>
            </ng-template>

            @if (accountService.accounts().length === 0) {
              <nz-empty nzNotFoundImage="simple" nzNotFoundContent="No accounts added yet.">
                <a routerLink="/accounts" nz-button nzType="primary" class="mt-2 bg-emerald-600 border-0 rounded-xl text-sm">
                  Add Account
                </a>
              </nz-empty>
            } @else {
              <div class="space-y-2.5">
                @for (acc of accountService.accounts(); track acc.id) {
                  <div class="flex items-center justify-between p-3 rounded-xl bg-slate-50/80 border border-slate-100 hover:bg-slate-100/70 transition-colors">
                    <div class="flex items-center gap-2.5 overflow-hidden pr-2">
                      <div class="w-7 h-7 rounded-lg bg-white shadow-2xs flex items-center justify-center text-slate-600 text-xs flex-shrink-0">
                        <span nz-icon [nzType]="acc.type === 3 ? 'credit-card' : 'bank'"></span>
                      </div>
                      <div class="overflow-hidden">
                        <span class="font-medium text-slate-800 text-xs block truncate">{{ acc.name }}</span>
                        <span class="text-[10px] text-slate-400 block">{{ acc.accountNumberMask || 'Active' }}</span>
                      </div>
                    </div>
                    <span class="font-bold text-slate-900 text-xs whitespace-nowrap">
                      {{ acc.currentBalancePaise | inrCurrency }}
                    </span>
                  </div>
                }
              </div>
            }
          </nz-card>

          <!-- Upcoming Bills Card -->
          <nz-card [nzTitle]="'Upcoming Bills'" [nzExtra]="viewBillsTpl" class="rounded-2xl border border-slate-200/80 shadow-2xs">
            <ng-template #viewBillsTpl>
              <a routerLink="/bills" class="text-xs font-semibold text-emerald-600 hover:text-emerald-500">View All →</a>
            </ng-template>

            @if (upcomingBills().length === 0) {
              <nz-empty nzNotFoundImage="simple" nzNotFoundContent="No upcoming bills due.">
                <a routerLink="/bills" nz-button nzType="default" class="mt-2 rounded-xl text-xs">
                  Schedule Bill
                </a>
              </nz-empty>
            } @else {
              <div class="space-y-2.5">
                @for (bill of upcomingBills(); track bill.id) {
                  <div class="flex items-center justify-between p-2.5 rounded-xl hover:bg-slate-50 transition-colors border border-transparent hover:border-slate-100">
                    <div class="flex items-center gap-2 overflow-hidden pr-2">
                      <div class="w-7 h-7 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center text-xs flex-shrink-0">
                        <span nz-icon nzType="calendar"></span>
                      </div>
                      <div class="overflow-hidden">
                        <div class="flex items-center gap-1.5">
                          <span class="font-medium text-slate-800 text-xs truncate">{{ bill.billName }}</span>
                          @if (bill.status === 3) {
                            <nz-tag nzColor="error" class="rounded-full text-[9px] px-1.5 m-0 leading-tight">Overdue</nz-tag>
                          }
                        </div>
                        <span class="text-[11px] text-slate-400 font-mono">Due {{ bill.dueDate }}</span>
                      </div>
                    </div>
                    <div class="text-right flex-shrink-0">
                      <span class="font-bold text-slate-900 text-xs block">
                        {{ bill.amountPaise | inrCurrency }}
                      </span>
                      <a routerLink="/bills" class="text-[11px] text-emerald-600 hover:underline">Pay →</a>
                    </div>
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
  billService = inject(BillService);

  recentTransactions = signal<Transaction[]>([]);
  upcomingBills = signal<BillOccurrence[]>([]);

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
    this.billService.loadOccurrences().subscribe(res => {
      this.upcomingBills.set(res.filter(o => o.status !== 2).slice(0, 4));
    });
  }
}
