import { Component, OnInit, signal, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  Validators,
} from "@angular/forms";
import { NzTableModule } from "ng-zorro-antd/table";
import { NzCardModule } from "ng-zorro-antd/card";
import { NzButtonModule } from "ng-zorro-antd/button";
import { NzModalModule } from "ng-zorro-antd/modal";
import { NzFormModule } from "ng-zorro-antd/form";
import { NzInputModule } from "ng-zorro-antd/input";
import { NzSelectModule } from "ng-zorro-antd/select";
import { NzInputNumberModule } from "ng-zorro-antd/input-number";
import { NzDatePickerModule } from "ng-zorro-antd/date-picker";
import { NzTagModule } from "ng-zorro-antd/tag";
import { NzPopconfirmModule } from "ng-zorro-antd/popconfirm";
import { NzSwitchModule } from "ng-zorro-antd/switch";
import { NzCheckboxModule } from "ng-zorro-antd/checkbox";
import { NzIconModule } from "ng-zorro-antd/icon";
import { NzMessageService } from "ng-zorro-antd/message";
import { InrCurrencyPipe } from "../../shared/pipes/inr-currency.pipe";
import { TransactionImportModalComponent } from "./transaction-import-modal.component";
import {
  TransactionService,
  Transaction,
  Category,
} from "../../core/services/transaction.service";
import { AccountService } from "../../core/services/account.service";

@Component({
  selector: "app-transactions",
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    NzTableModule,
    NzCardModule,
    NzButtonModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzTagModule,
    NzPopconfirmModule,
    NzSwitchModule,
    NzCheckboxModule,
    NzIconModule,
    InrCurrencyPipe,
    TransactionImportModalComponent,
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">
            Transactions Ledger
          </h1>
          <p class="text-sm text-slate-500 mt-1">
            Audit log of all income, expenditures, splits, and account transfers
          </p>
        </div>
        <div class="flex items-center gap-2 flex-wrap">
          <button
            nz-button
            nzType="default"
            (click)="exportToCsv()"
            class="rounded-xl text-xs sm:text-sm font-medium border-slate-200 text-slate-700 bg-white hover:bg-slate-50 shadow-2xs flex items-center gap-1.5"
          >
            <span nz-icon nzType="download"></span>
            <span>Export CSV</span>
          </button>
          <button
            nz-button
            nzType="default"
            (click)="isImportModalVisible = true"
            class="rounded-xl text-xs sm:text-sm font-medium border-slate-200 text-slate-700 bg-white hover:bg-slate-50 shadow-2xs flex items-center gap-1.5"
          >
            <span nz-icon nzType="upload"></span>
            <span>Import Excel / CSV</span>
          </button>
          <button
            nz-button
            nzType="default"
            (click)="openTransferModal()"
            class="rounded-xl text-xs sm:text-sm font-medium border-slate-200 text-slate-700 bg-white hover:bg-slate-50 shadow-2xs flex items-center gap-1.5"
          >
            <span nz-icon nzType="swap"></span>
            <span>Record Transfer</span>
          </button>
          <button
            nz-button
            nzType="primary"
            (click)="openAddTxnModal()"
            class="rounded-xl text-xs sm:text-sm font-medium bg-emerald-600 hover:bg-emerald-500 border-0 shadow-2xs flex items-center gap-1.5"
          >
            <span nz-icon nzType="plus"></span>
            <span>Add Transaction</span>
          </button>
        </div>
      </div>

      <!-- Filters Bar -->
      <nz-card class="rounded-xl border border-slate-200/80 shadow-xs">
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
          <!-- Search Input -->
          <div class="lg:col-span-2">
            <label class="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1.5">
              Search Payee, Notes, Account
            </label>
            <div class="relative">
              <input
                nz-input
                [(ngModel)]="searchQuery"
                (ngModelChange)="onSearchInput($event)"
                placeholder="Search Swiggy, Salary, HDFC, Amazon..."
                class="rounded-lg h-9 pl-9 pr-8" />
              <span nz-icon nzType="search" class="absolute left-3 top-2.5 text-slate-400"></span>
              @if (searchQuery) {
                <span nz-icon nzType="close" (click)="clearSearch()" class="absolute right-3 top-2.5 text-slate-400 hover:text-slate-600 cursor-pointer"></span>
              }
            </div>
          </div>

          <!-- Type Filter -->
          <div>
            <label class="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1.5">Type</label>
            <nz-select [(ngModel)]="selectedTypeFilter" (ngModelChange)="onFilterChange()" class="w-full h-9">
              <nz-option [nzValue]="null" nzLabel="All Types"></nz-option>
              <nz-option [nzValue]="1" nzLabel="Expenses"></nz-option>
              <nz-option [nzValue]="2" nzLabel="Income"></nz-option>
              <nz-option [nzValue]="3" nzLabel="Transfers"></nz-option>
            </nz-select>
          </div>

          <!-- Account Filter -->
          <div>
            <label class="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1.5">Account</label>
            <nz-select [(ngModel)]="selectedAccountFilter" (ngModelChange)="onFilterChange()" class="w-full h-9" nzAllowClear nzPlaceHolder="All Accounts">
              @for (acc of accountService.accounts(); track acc.id) {
                <nz-option [nzValue]="acc.id" [nzLabel]="acc.name"></nz-option>
              }
            </nz-select>
          </div>

          <!-- Category Filter -->
          <div>
            <label class="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1.5">Category</label>
            <nz-select [(ngModel)]="selectedCategoryFilter" (ngModelChange)="onFilterChange()" class="w-full h-9" nzAllowClear nzPlaceHolder="All Categories">
              @for (cat of transactionService.categories(); track cat.id) {
                <nz-option [nzValue]="cat.id" [nzLabel]="cat.icon ? cat.icon + ' ' + cat.name : cat.name"></nz-option>
              }
            </nz-select>
          </div>
        </div>

        <!-- Date Range Filter Row -->
        <div class="mt-3 pt-3 border-t border-slate-100 flex flex-wrap items-center justify-between gap-3 text-xs">
          <div class="flex items-center gap-1.5 flex-wrap">
            <span class="text-slate-400 font-medium mr-1">Period:</span>
            <button
              nz-button
              nzSize="small"
              [nzType]="activeDatePreset === 'all' ? 'primary' : 'default'"
              (click)="setDatePreset('all')"
              class="rounded-md text-xs">
              All Time
            </button>
            <button
              nz-button
              nzSize="small"
              [nzType]="activeDatePreset === 'this_month' ? 'primary' : 'default'"
              (click)="setDatePreset('this_month')"
              class="rounded-md text-xs">
              This Month
            </button>
            <button
              nz-button
              nzSize="small"
              [nzType]="activeDatePreset === 'last_month' ? 'primary' : 'default'"
              (click)="setDatePreset('last_month')"
              class="rounded-md text-xs">
              Last Month
            </button>
            <button
              nz-button
              nzSize="small"
              [nzType]="activeDatePreset === 'last_30' ? 'primary' : 'default'"
              (click)="setDatePreset('last_30')"
              class="rounded-md text-xs">
              Last 30 Days
            </button>
          </div>

          @if (hasActiveFilters()) {
            <button
              nz-button
              nzType="text"
              nzSize="small"
              (click)="resetFilters()"
              class="text-rose-600 hover:text-rose-700 text-xs font-medium">
              Reset All Filters
            </button>
          }
        </div>
      </nz-card>

      <!-- Sticky / Floating Bulk Action Toolbar -->
      @if (selectedIds.size > 0) {
        <div class="bg-slate-900 text-white rounded-xl p-3 shadow-lg flex flex-wrap items-center justify-between gap-3">
          <div class="flex items-center gap-2">
            <span class="w-6 h-6 rounded-full bg-emerald-500 text-white flex items-center justify-center text-xs font-bold font-mono">
              {{ selectedIds.size }}
            </span>
            <span class="text-sm font-medium">transactions selected</span>
          </div>

          <div class="flex items-center gap-2 flex-wrap">
            <!-- Bulk Category Selection -->
            <nz-select
              [(ngModel)]="bulkCategoryId"
              nzPlaceHolder="Assign Category"
              nzAllowClear
              class="w-48 text-slate-800 text-xs">
              @for (cat of transactionService.categories(); track cat.id) {
                <nz-option [nzValue]="cat.id" [nzLabel]="cat.icon ? cat.icon + ' ' + cat.name : cat.name"></nz-option>
              }
            </nz-select>
            <button
              nz-button
              nzType="primary"
              nzSize="small"
              [nzLoading]="isBulkProcessing"
              [disabled]="!bulkCategoryId"
              (click)="applyBulkCategory()"
              class="bg-emerald-600 hover:bg-emerald-500 border-0 rounded-lg text-xs">
              Apply Category
            </button>

            <!-- Bulk Visibility Selection -->
            <nz-select
              [(ngModel)]="bulkVisibilityValue"
              class="w-36 text-slate-800 text-xs">
              <nz-option [nzValue]="1" nzLabel="Mark Shared"></nz-option>
              <nz-option [nzValue]="2" nzLabel="Mark Private"></nz-option>
            </nz-select>
            <button
              nz-button
              nzType="default"
              nzSize="small"
              [nzLoading]="isBulkProcessing"
              (click)="applyBulkVisibility()"
              class="rounded-lg text-xs text-slate-800">
              Apply Visibility
            </button>

            <!-- Bulk Delete -->
            <button
              nz-button
              nzType="primary"
              nzDanger
              nzSize="small"
              [nzLoading]="isBulkProcessing"
              nz-popconfirm
              [nzPopconfirmTitle]="'Delete ' + selectedIds.size + ' transactions? Account balances will be reversed.'"
              (nzOnConfirm)="applyBulkDelete()"
              class="rounded-lg text-xs flex items-center gap-1">
              <span nz-icon nzType="delete"></span>
              <span>Delete ({{ selectedIds.size }})</span>
            </button>

            <!-- Clear selection -->
            <button
              nz-button
              nzType="text"
              nzSize="small"
              (click)="clearSelection()"
              class="text-slate-400 hover:text-white text-xs">
              ✕ Deselect
            </button>
          </div>
        </div>
      }

      <!-- Transactions Table -->
      <nz-card class="rounded-xl border border-slate-200/80 shadow-xs overflow-hidden">
        <nz-table
          #table
          [nzData]="transactionService.transactions()"
          [nzTotal]="transactionService.totalCount()"
          [nzPageIndex]="page"
          [nzPageSize]="pageSize"
          [nzFrontPagination]="false"
          [nzLoading]="transactionService.loading()"
          [nzShowSizeChanger]="true"
          [nzPageSizeOptions]="[10, 20, 50, 100]"
          [nzShowTotal]="totalTpl"
          (nzPageIndexChange)="onPageChange($event)"
          (nzPageSizeChange)="onPageSizeChange($event)"
          [nzBordered]="false"
          nzSize="middle"
        >
          <thead>
            <tr class="bg-slate-50 text-xs font-semibold text-slate-500 uppercase tracking-wider">
              <th nzWidth="40px" nzAlign="center">
                <label
                  nz-checkbox
                  [nzChecked]="isAllCurrentPageSelected()"
                  [nzIndeterminate]="isPartiallySelected()"
                  (nzCheckedChange)="onSelectAllCurrentPage($event)">
                </label>
              </th>
              <th nzWidth="120px">Date</th>
              <th>Payee / Description</th>
              <th>Category</th>
              <th>Account</th>
              <th>Type</th>
              <th>Visibility</th>
              <th nzAlign="right">Amount</th>
              <th nzWidth="80px" nzAlign="center">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (txn of table.data; track txn.id) {
              <tr class="hover:bg-slate-50/60 transition-colors" [class.bg-emerald-50]="selectedIds.has(txn.id)">
                <td nzAlign="center">
                  <label
                    nz-checkbox
                    [nzChecked]="selectedIds.has(txn.id)"
                    (nzCheckedChange)="onToggleRowSelection(txn.id, $event)">
                  </label>
                </td>
                <td class="text-slate-600 font-mono text-xs whitespace-nowrap">
                  {{ txn.date }}
                </td>
                <td>
                  <div class="font-medium text-slate-900 text-sm">
                    {{
                      txn.payee ||
                        (txn.transactionType === 3
                          ? "Account Transfer"
                          : "Direct Transaction")
                    }}
                  </div>
                  @if (txn.notes) {
                    <div class="text-xs text-slate-400 mt-0.5 line-clamp-1">
                      {{ txn.notes }}
                    </div>
                  }
                  @if (txn.isSplit && txn.splits) {
                    <div class="mt-1 space-y-0.5">
                      @for (s of txn.splits; track s.categoryId) {
                        <div class="text-[11px] text-slate-500 flex items-center gap-1.5">
                          <span class="w-1.5 h-1.5 rounded-full bg-slate-400"></span>
                          <span>{{ s.categoryName || "Split" }}:</span>
                          <span class="font-mono font-medium">{{ s.amountPaise | inrCurrency }}</span>
                        </div>
                      }
                    </div>
                  }
                </td>
                <td>
                  @if (txn.categoryName) {
                    <nz-tag class="rounded text-xs">{{ txn.categoryName }}</nz-tag>
                  } @else if (txn.transactionType === 3) {
                    <nz-tag nzColor="default" class="rounded text-xs">Transfer</nz-tag>
                  } @else {
                    <span class="text-slate-400 text-xs">—</span>
                  }
                </td>
                <td class="text-slate-700 text-xs font-medium">
                  {{ txn.accountName }}
                </td>
                <td>
                  @switch (txn.transactionType) {
                    @case (1) {
                      <nz-tag nzColor="error" class="rounded text-[11px]">Expense</nz-tag>
                    }
                    @case (2) {
                      <nz-tag nzColor="success" class="rounded text-[11px]">Income</nz-tag>
                    }
                    @case (3) {
                      <nz-tag nzColor="blue" class="rounded text-[11px]">Transfer</nz-tag>
                    }
                  }
                </td>
                <td>
                  @if (txn.visibility === 2) {
                    <nz-tag nzColor="magenta" class="rounded text-[10px] flex items-center gap-0.5">
                      <span nz-icon nzType="lock"></span> Private
                    </nz-tag>
                  } @else {
                    <nz-tag nzColor="default" class="rounded text-[10px] flex items-center gap-0.5">
                      <span nz-icon nzType="user"></span> Shared
                    </nz-tag>
                  }
                </td>
                <td nzAlign="right">
                  <span
                    class="font-mono font-bold text-sm tracking-tight whitespace-nowrap"
                    [ngClass]="{
                      'text-rose-600': txn.transactionType === 1,
                      'text-emerald-600': txn.transactionType === 2,
                      'text-blue-600': txn.transactionType === 3,
                    }"
                  >
                    {{
                      txn.transactionType === 1
                        ? "-"
                        : txn.transactionType === 2
                          ? "+"
                          : ""
                    }}
                    {{ txn.amountPaise | inrCurrency }}
                  </span>
                </td>
                <td nzAlign="center">
                  <button
                    nz-button
                    nzType="text"
                    nzDanger
                    nz-popconfirm
                    nzPopconfirmTitle="Delete this transaction? The account balance will be reversed."
                    (nzOnConfirm)="deleteTransaction(txn.id)"
                    class="text-xs p-1 text-slate-400 hover:text-rose-600 flex items-center justify-center"
                  >
                    <span nz-icon nzType="delete"></span>
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </nz-table>
        <ng-template #totalTpl let-total let-range="range">
          <span class="text-xs text-slate-500 font-mono">
            Showing {{ range[0] }}-{{ range[1] }} of {{ total }} transactions
          </span>
        </ng-template>
      </nz-card>

      <!-- Add Transaction Modal -->
      <nz-modal
        [(nzVisible)]="isAddModalVisible"
        nzTitle="Record Transaction"
        [nzOkText]="'Save Transaction'"
        [nzOkLoading]="isSubmitting"
        [nzWidth]="600"
        (nzOnCancel)="closeAddModal()"
        (nzOnOk)="submitTransaction()"
      >
        <ng-container *nzModalContent>
          <form [formGroup]="txnForm" nz-form nzLayout="vertical" class="space-y-3 pt-2">
            <!-- Type Selector -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Transaction Type</nz-form-label>
              <nz-form-control>
                <nz-select formControlName="transactionType">
                  <nz-option [nzValue]="1" nzLabel="Expense"></nz-option>
                  <nz-option [nzValue]="2" nzLabel="Income"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-3">
              <!-- Account -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Account</nz-form-label>
                <nz-form-control nzErrorTip="Account is required">
                  <nz-select formControlName="accountId" nzPlaceHolder="Select account">
                    @for (acc of accountService.accounts(); track acc.id) {
                      <nz-option [nzValue]="acc.id" [nzLabel]="acc.name"></nz-option>
                    }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <!-- Category (if not split) -->
              @if (!isSplitActive) {
                <nz-form-item class="mb-3">
                  <nz-form-label>Category</nz-form-label>
                  <nz-form-control>
                    <nz-select formControlName="categoryId" nzPlaceHolder="Select category" nzAllowClear>
                      @for (cat of transactionService.categories(); track cat.id) {
                        <nz-option [nzValue]="cat.id" [nzLabel]="cat.icon ? cat.icon + ' ' + cat.name : cat.name"></nz-option>
                      }
                    </nz-select>
                  </nz-form-control>
                </nz-form-item>
              }
            </div>

            <div class="grid grid-cols-2 gap-3">
              <!-- Amount -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Amount (₹)</nz-form-label>
                <nz-form-control nzErrorTip="Amount must be greater than zero">
                  <nz-input-number formControlName="amountRupees" [nzMin]="0.01" [nzStep]="10" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Date -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="date" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <!-- Payee -->
              <nz-form-item class="mb-3">
                <nz-form-label>Payee / Merchant</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="payee" placeholder="e.g. Swiggy, Amazon, Employer" />
                </nz-form-control>
              </nz-form-item>

              <!-- Visibility -->
              <nz-form-item class="mb-3">
                <nz-form-label>Visibility</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="visibility">
                    <nz-option [nzValue]="1" nzLabel="Household (Shared)"></nz-option>
                    <nz-option [nzValue]="2" nzLabel="Private (Only Me)"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <!-- Notes -->
            <nz-form-item class="mb-3">
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <input nz-input formControlName="notes" placeholder="Optional notes or receipt references" />
              </nz-form-control>
            </nz-form-item>

            <!-- Split Toggle -->
            <div class="pt-2 border-t border-slate-100 flex items-center justify-between">
              <span class="text-sm font-medium text-slate-700">Split into multiple categories</span>
              <nz-switch [(ngModel)]="isSplitActive" [ngModelOptions]="{ standalone: true }"></nz-switch>
            </div>

            <!-- Split Items Editor -->
            @if (isSplitActive) {
              <div class="bg-slate-50 p-3 rounded-xl space-y-2 border border-slate-200">
                <div class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">Category Splits</div>
                @for (split of splitRows; track $index; let i = $index) {
                  <div class="flex items-center gap-2">
                    <nz-select
                      [(ngModel)]="split.categoryId"
                      [ngModelOptions]="{ standalone: true }"
                      class="flex-1"
                      nzPlaceHolder="Select category"
                    >
                      @for (cat of transactionService.categories(); track cat.id) {
                        <nz-option [nzValue]="cat.id" [nzLabel]="cat.icon ? cat.icon + ' ' + cat.name : cat.name"></nz-option>
                      }
                    </nz-select>
                    <nz-input-number
                      [(ngModel)]="split.amountRupees"
                      [ngModelOptions]="{ standalone: true }"
                      [nzMin]="0.01"
                      placeholder="₹"
                      class="w-32"
                    ></nz-input-number>
                    @if (splitRows.length > 2) {
                      <button nz-button nzType="text" nzDanger (click)="removeSplitRow(i)">✕</button>
                    }
                  </div>
                }
                <button nz-button nzType="dashed" nzBlock (click)="addSplitRow()" class="mt-2 text-xs">
                  + Add Another Split
                </button>
              </div>
            }
          </form>
        </ng-container>
      </nz-modal>

      <!-- Record Transfer Modal -->
      <nz-modal
        [(nzVisible)]="isTransferModalVisible"
        nzTitle="Record Account Transfer"
        [nzOkText]="'Execute Transfer'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closeTransferModal()"
        (nzOnOk)="submitTransfer()"
      >
        <ng-container *nzModalContent>
          <form [formGroup]="transferForm" nz-form nzLayout="vertical" class="space-y-3 pt-2">
            <div class="bg-blue-50 border border-blue-200 rounded-lg p-3 text-xs text-blue-800 mb-3">
              💡 Transfers shift funds between your owned accounts. They do <strong>not</strong> count as income or expense in household totals.
            </div>

            <!-- Source Account -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Source Account (From)</nz-form-label>
              <nz-form-control nzErrorTip="Source account is required">
                <nz-select formControlName="sourceAccountId" nzPlaceHolder="Select source account">
                  @for (acc of accountService.accounts(); track acc.id) {
                    <nz-option [nzValue]="acc.id" [nzLabel]="acc.name + ' (' + (acc.currentBalancePaise | inrCurrency) + ')'"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <!-- Destination Account -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Destination Account (To)</nz-form-label>
              <nz-form-control nzErrorTip="Destination account is required">
                <nz-select formControlName="destinationAccountId" nzPlaceHolder="Select destination account">
                  @for (acc of accountService.accounts(); track acc.id) {
                    <nz-option [nzValue]="acc.id" [nzLabel]="acc.name + ' (' + (acc.currentBalancePaise | inrCurrency) + ')'"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-3">
              <!-- Amount -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Amount (₹)</nz-form-label>
                <nz-form-control nzErrorTip="Amount must be greater than zero">
                  <nz-input-number formControlName="amountRupees" [nzMin]="0.01" [nzStep]="100" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Date -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="date" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <!-- Notes -->
            <nz-form-item class="mb-3">
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <input nz-input formControlName="notes" placeholder="e.g. Credit card settlement, monthly savings deposit" />
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Import Excel / CSV Modal -->
      <app-transaction-import-modal
        [(isVisible)]="isImportModalVisible"
        (importCompleted)="loadData()">
      </app-transaction-import-modal>
    </div>
  `,
})
export class TransactionsComponent implements OnInit {
  transactionService = inject(TransactionService);
  accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private msg = inject(NzMessageService);

  page = 1;
  pageSize = 20;
  searchQuery = "";
  selectedTypeFilter: number | null = null;
  selectedAccountFilter: string | null = null;
  selectedCategoryFilter: string | null = null;
  activeDatePreset: "all" | "this_month" | "last_month" | "last_30" = "all";
  startDateFilter?: string;
  endDateFilter?: string;

  private searchTimeout: any;

  // Bulk Selection State
  selectedIds = new Set<string>();
  bulkCategoryId: string | null = null;
  bulkVisibilityValue: number = 1;
  isBulkProcessing = false;

  isImportModalVisible = false;
  isAddModalVisible = false;
  isTransferModalVisible = false;
  isSubmitting = false;
  isSplitActive = false;

  splitRows: { categoryId: string; amountRupees: number }[] = [
    { categoryId: "", amountRupees: 0 },
    { categoryId: "", amountRupees: 0 },
  ];

  txnForm = this.fb.group({
    transactionType: [1, [Validators.required]],
    accountId: ["", [Validators.required]],
    categoryId: [""],
    amountRupees: [0, [Validators.required, Validators.min(0.01)]],
    date: [new Date(), [Validators.required]],
    payee: [""],
    notes: [""],
    visibility: [1, [Validators.required]],
  });

  transferForm = this.fb.group({
    sourceAccountId: ["", [Validators.required]],
    destinationAccountId: ["", [Validators.required]],
    amountRupees: [0, [Validators.required, Validators.min(0.01)]],
    date: [new Date(), [Validators.required]],
    notes: [""],
  });

  ngOnInit(): void {
    this.accountService.loadAccounts().subscribe();
    this.transactionService.loadCategories().subscribe();
    this.loadData();
  }

  loadData(): void {
    this.transactionService
      .loadTransactions({
        page: this.page,
        pageSize: this.pageSize,
        type: this.selectedTypeFilter || undefined,
        accountId: this.selectedAccountFilter || undefined,
        categoryId: this.selectedCategoryFilter || undefined,
        search: this.searchQuery || undefined,
        startDate: this.startDateFilter,
        endDate: this.endDateFilter,
      })
      .subscribe();
  }

  onSearchInput(val: string): void {
    if (this.searchTimeout) clearTimeout(this.searchTimeout);
    this.searchTimeout = setTimeout(() => {
      this.page = 1;
      this.loadData();
    }, 300);
  }

  clearSearch(): void {
    this.searchQuery = "";
    this.page = 1;
    this.loadData();
  }

  setDatePreset(preset: "all" | "this_month" | "last_month" | "last_30"): void {
    this.activeDatePreset = preset;
    const now = new Date();
    if (preset === "all") {
      this.startDateFilter = undefined;
      this.endDateFilter = undefined;
    } else if (preset === "this_month") {
      const start = new Date(now.getFullYear(), now.getMonth(), 1);
      const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
      this.startDateFilter = start.toISOString().split("T")[0];
      this.endDateFilter = end.toISOString().split("T")[0];
    } else if (preset === "last_month") {
      const start = new Date(now.getFullYear(), now.getMonth() - 1, 1);
      const end = new Date(now.getFullYear(), now.getMonth(), 0);
      this.startDateFilter = start.toISOString().split("T")[0];
      this.endDateFilter = end.toISOString().split("T")[0];
    } else if (preset === "last_30") {
      const start = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
      this.startDateFilter = start.toISOString().split("T")[0];
      this.endDateFilter = now.toISOString().split("T")[0];
    }
    this.page = 1;
    this.loadData();
  }

  hasActiveFilters(): boolean {
    return !!(
      this.searchQuery ||
      this.selectedTypeFilter ||
      this.selectedAccountFilter ||
      this.selectedCategoryFilter ||
      this.activeDatePreset !== "all"
    );
  }

  resetFilters(): void {
    this.searchQuery = "";
    this.selectedTypeFilter = null;
    this.selectedAccountFilter = null;
    this.selectedCategoryFilter = null;
    this.activeDatePreset = "all";
    this.startDateFilter = undefined;
    this.endDateFilter = undefined;
    this.page = 1;
    this.loadData();
  }

  onFilterChange(): void {
    this.page = 1;
    this.loadData();
  }

  onPageChange(p: number): void {
    this.page = p;
    this.loadData();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.page = 1;
    this.loadData();
  }

  // --- Bulk Selection ---
  isAllCurrentPageSelected(): boolean {
    const list = this.transactionService.transactions();
    if (list.length === 0) return false;
    return list.every((t) => this.selectedIds.has(t.id));
  }

  isPartiallySelected(): boolean {
    const list = this.transactionService.transactions();
    if (list.length === 0) return false;
    const some = list.some((t) => this.selectedIds.has(t.id));
    return some && !this.isAllCurrentPageSelected();
  }

  onSelectAllCurrentPage(checked: boolean): void {
    const list = this.transactionService.transactions();
    if (checked) {
      list.forEach((t) => this.selectedIds.add(t.id));
    } else {
      list.forEach((t) => this.selectedIds.delete(t.id));
    }
  }

  onToggleRowSelection(id: string, checked: boolean): void {
    if (checked) {
      this.selectedIds.add(id);
    } else {
      this.selectedIds.delete(id);
    }
  }

  clearSelection(): void {
    this.selectedIds.clear();
    this.bulkCategoryId = null;
  }

  applyBulkCategory(): void {
    if (this.selectedIds.size === 0) return;
    this.isBulkProcessing = true;
    this.transactionService
      .bulkCategorize(Array.from(this.selectedIds), this.bulkCategoryId)
      .subscribe({
        next: (res) => {
          this.msg.success(
            `Category updated for ${res.affectedCount} transactions.`
          );
          this.isBulkProcessing = false;
          this.clearSelection();
          this.loadData();
        },
        error: (err) => {
          this.isBulkProcessing = false;
          this.msg.error(err.error?.detail || "Failed to update categories.");
        },
      });
  }

  applyBulkVisibility(): void {
    if (this.selectedIds.size === 0) return;
    this.isBulkProcessing = true;
    this.transactionService
      .bulkVisibility(Array.from(this.selectedIds), this.bulkVisibilityValue)
      .subscribe({
        next: (res) => {
          this.msg.success(
            `Visibility updated for ${res.affectedCount} transactions.`
          );
          this.isBulkProcessing = false;
          this.clearSelection();
          this.loadData();
        },
        error: (err) => {
          this.isBulkProcessing = false;
          this.msg.error(err.error?.detail || "Failed to update visibility.");
        },
      });
  }

  applyBulkDelete(): void {
    if (this.selectedIds.size === 0) return;
    this.isBulkProcessing = true;
    this.transactionService
      .bulkDelete(Array.from(this.selectedIds))
      .subscribe({
        next: (res) => {
          this.msg.success(`Deleted ${res.affectedCount} transactions.`);
          this.isBulkProcessing = false;
          this.clearSelection();
          this.accountService.loadAccounts().subscribe();
          this.loadData();
        },
        error: (err) => {
          this.isBulkProcessing = false;
          this.msg.error(err.error?.detail || "Failed to delete transactions.");
        },
      });
  }

  exportToCsv(): void {
    const list = this.transactionService.transactions();
    if (list.length === 0) {
      this.msg.warning("No transactions to export.");
      return;
    }

    const headers = [
      "Date",
      "Payee / Description",
      "Category",
      "Account",
      "Type",
      "Amount (INR)",
      "Notes",
      "Visibility",
    ];

    const rows = list.map((t) => [
      t.date,
      `"${(t.payee || "").replace(/"/g, '""')}"`,
      `"${(t.categoryName || "").replace(/"/g, '""')}"`,
      `"${(t.accountName || "").replace(/"/g, '""')}"`,
      t.transactionType === 1
        ? "Expense"
        : t.transactionType === 2
          ? "Income"
          : "Transfer",
      (t.amountPaise / 100).toFixed(2),
      `"${(t.notes || "").replace(/"/g, '""')}"`,
      t.visibility === 2 ? "Private" : "Household",
    ]);

    const csvContent = [headers.join(","), ...rows.map((r) => r.join(","))].join(
      "\r\n"
    );
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `Finora_Transactions_${new Date().toISOString().split("T")[0]}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    this.msg.success(`Exported ${list.length} transactions to CSV.`);
  }

  // --- Add / Transfer Modals ---
  openAddTxnModal(): void {
    this.isSplitActive = false;
    this.splitRows = [
      { categoryId: "", amountRupees: 0 },
      { categoryId: "", amountRupees: 0 },
    ];
    this.txnForm.reset({
      transactionType: 1,
      accountId: this.accountService.accounts()[0]?.id || "",
      categoryId: "",
      amountRupees: 0,
      date: new Date(),
      payee: "",
      notes: "",
      visibility: 1,
    });
    this.isAddModalVisible = true;
  }

  closeAddModal(): void {
    this.isAddModalVisible = false;
  }

  addSplitRow(): void {
    this.splitRows.push({ categoryId: "", amountRupees: 0 });
  }

  removeSplitRow(index: number): void {
    this.splitRows.splice(index, 1);
  }

  submitTransaction(): void {
    if (this.txnForm.invalid) {
      Object.values(this.txnForm.controls).forEach((c) => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.txnForm.value;
    const amountPaise = Math.round(Number(val.amountRupees) * 100);

    let splitsPayload:
      | { categoryId: string; amountPaise: number }[]
      | undefined;

    if (this.isSplitActive) {
      const invalidSplit = this.splitRows.some(
        (s) => !s.categoryId || s.amountRupees <= 0
      );
      if (invalidSplit) {
        this.msg.error(
          "Please select a category and positive amount for each split item."
        );
        return;
      }

      const totalSplitPaise = this.splitRows.reduce(
        (sum, s) => sum + Math.round(Number(s.amountRupees) * 100),
        0
      );

      if (totalSplitPaise !== amountPaise) {
        this.msg.error(
          `Split amounts sum to ₹${totalSplitPaise / 100} but total amount is ₹${amountPaise / 100}.`
        );
        return;
      }

      splitsPayload = this.splitRows.map((s) => ({
        categoryId: s.categoryId,
        amountPaise: Math.round(Number(s.amountRupees) * 100),
      }));
    }

    const dateVal = val.date ? new Date(val.date) : new Date();
    const formattedDate = dateVal.toISOString().split("T")[0];

    this.isSubmitting = true;
    this.transactionService
      .createTransaction({
        accountId: val.accountId!,
        categoryId: this.isSplitActive
          ? undefined
          : val.categoryId || undefined,
        transactionType: Number(val.transactionType),
        amountPaise,
        date: formattedDate,
        payee: val.payee?.trim() || undefined,
        notes: val.notes?.trim() || undefined,
        visibility: Number(val.visibility),
        splits: splitsPayload,
      })
      .subscribe({
        next: () => {
          this.msg.success("Transaction recorded successfully");
          this.isSubmitting = false;
          this.isAddModalVisible = false;
          this.accountService.loadAccounts().subscribe();
          this.loadData();
        },
        error: (err) => {
          this.msg.error(
            err.error?.detail || "Failed to record transaction"
          );
          this.isSubmitting = false;
        },
      });
  }

  openTransferModal(): void {
    const accs = this.accountService.accounts();
    this.transferForm.reset({
      sourceAccountId: accs[0]?.id || "",
      destinationAccountId: accs[1]?.id || "",
      amountRupees: 0,
      date: new Date(),
      notes: "",
    });
    this.isTransferModalVisible = true;
  }

  closeTransferModal(): void {
    this.isTransferModalVisible = false;
  }

  submitTransfer(): void {
    if (this.transferForm.invalid) {
      Object.values(this.transferForm.controls).forEach((c) => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.transferForm.value;
    if (val.sourceAccountId === val.destinationAccountId) {
      this.msg.error("Source and destination accounts must be different.");
      return;
    }

    const amountPaise = Math.round(Number(val.amountRupees) * 100);
    const dateVal = val.date ? new Date(val.date) : new Date();
    const formattedDate = dateVal.toISOString().split("T")[0];

    this.isSubmitting = true;
    this.transactionService
      .recordTransfer({
        sourceAccountId: val.sourceAccountId!,
        destinationAccountId: val.destinationAccountId!,
        amountPaise,
        date: formattedDate,
        notes: val.notes?.trim() || undefined,
      })
      .subscribe({
        next: () => {
          this.msg.success("Transfer recorded and balances adjusted");
          this.isSubmitting = false;
          this.isTransferModalVisible = false;
          this.accountService.loadAccounts().subscribe();
          this.loadData();
        },
        error: (err) => {
          this.msg.error(err.error?.detail || "Failed to record transfer");
          this.isSubmitting = false;
        },
      });
  }

  deleteTransaction(id: string): void {
    this.transactionService.deleteTransaction(id).subscribe({
      next: () => {
        this.msg.success("Transaction deleted and balance restored");
        this.selectedIds.delete(id);
        this.accountService.loadAccounts().subscribe();
      },
      error: () => this.msg.error("Failed to delete transaction"),
    });
  }
}
