import { Component, OnInit, signal, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormArray,
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
import { NzIconModule } from "ng-zorro-antd/icon";
import { NzMessageService } from "ng-zorro-antd/message";
import { InrCurrencyPipe } from "../../shared/pipes/inr-currency.pipe";
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
    NzIconModule,
    InrCurrencyPipe,
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto">
      <!-- Header -->
      <div
        class="flex flex-col sm:flex-row sm:items-center justify-between gap-4"
      >
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">
            Transactions Ledger
          </h1>
          <p class="text-sm text-slate-500 mt-1">
            Audit log of all income, expenditures, splits, and account transfers
          </p>
        </div>
        <div class="flex items-center gap-2">
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
        <div class="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 gap-4">
          <!-- Type Filter -->
          <div>
            <label
              class="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1.5"
              >Type</label
            >
            <nz-select
              [(ngModel)]="selectedTypeFilter"
              (ngModelChange)="onFilterChange()"
              class="w-full"
            >
              <nz-option [nzValue]="null" nzLabel="All Types"></nz-option>
              <nz-option [nzValue]="1" nzLabel="Expenses"></nz-option>
              <nz-option [nzValue]="2" nzLabel="Income"></nz-option>
              <nz-option [nzValue]="3" nzLabel="Transfers"></nz-option>
            </nz-select>
          </div>

          <!-- Account Filter -->
          <div>
            <label
              class="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1.5"
              >Account</label
            >
            <nz-select
              [(ngModel)]="selectedAccountFilter"
              (ngModelChange)="onFilterChange()"
              class="w-full"
              nzAllowClear
              nzPlaceHolder="All Accounts"
            >
              @for (acc of accountService.accounts(); track acc.id) {
                <nz-option [nzValue]="acc.id" [nzLabel]="acc.name"></nz-option>
              }
            </nz-select>
          </div>

          <!-- Category Filter -->
          <div>
            <label
              class="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1.5"
              >Category</label
            >
            <nz-select
              [(ngModel)]="selectedCategoryFilter"
              (ngModelChange)="onFilterChange()"
              class="w-full"
              nzAllowClear
              nzPlaceHolder="All Categories"
            >
              @for (cat of transactionService.categories(); track cat.id) {
                <nz-option [nzValue]="cat.id" [nzLabel]="cat.name"> </nz-option>
              }
            </nz-select>
          </div>
        </div>
      </nz-card>

      <!-- Transactions Table -->
      <nz-card
        class="rounded-xl border border-slate-200/80 shadow-xs overflow-hidden"
      >
        <nz-table
          #table
          [nzData]="transactionService.transactions()"
          [nzTotal]="transactionService.totalCount()"
          [nzPageIndex]="page"
          [nzPageSize]="pageSize"
          [nzFrontPagination]="false"
          [nzLoading]="transactionService.loading()"
          (nzPageIndexChange)="onPageChange($event)"
          [nzBordered]="false"
          nzSize="middle"
        >
          <thead>
            <tr
              class="bg-slate-50 text-xs font-semibold text-slate-500 uppercase tracking-wider"
            >
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
              <tr class="hover:bg-slate-50/60 transition-colors">
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
                        <div
                          class="text-[11px] text-slate-500 flex items-center gap-1.5"
                        >
                          <span
                            class="w-1.5 h-1.5 rounded-full bg-slate-400"
                          ></span>
                          <span>{{ s.categoryName || "Split" }}:</span>
                          <span class="font-mono font-medium">{{
                            s.amountPaise | inrCurrency
                          }}</span>
                        </div>
                      }
                    </div>
                  }
                </td>
                <td>
                  @if (txn.categoryName) {
                    <nz-tag class="rounded text-xs">{{
                      txn.categoryName
                    }}</nz-tag>
                  } @else if (txn.transactionType === 3) {
                    <nz-tag nzColor="default" class="rounded text-xs"
                      >Transfer</nz-tag
                    >
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
                      <nz-tag nzColor="error" class="rounded text-[11px]"
                        >Expense</nz-tag
                      >
                    }
                    @case (2) {
                      <nz-tag nzColor="success" class="rounded text-[11px]"
                        >Income</nz-tag
                      >
                    }
                    @case (3) {
                      <nz-tag nzColor="blue" class="rounded text-[11px]"
                        >Transfer</nz-tag
                      >
                    }
                  }
                </td>
                <td>
                  @if (txn.visibility === 2) {
                    <nz-tag
                      nzColor="magenta"
                      class="rounded text-[10px] flex items-center gap-0.5"
                    >
                      <span nz-icon nzType="lock"></span> Private
                    </nz-tag>
                  } @else {
                    <nz-tag
                      nzColor="default"
                      class="rounded text-[10px] flex items-center gap-0.5"
                    >
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
          <form
            [formGroup]="txnForm"
            nz-form
            nzLayout="vertical"
            class="space-y-3 pt-2"
          >
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
                  <nz-select
                    formControlName="accountId"
                    nzPlaceHolder="Select account"
                  >
                    @for (acc of accountService.accounts(); track acc.id) {
                      <nz-option
                        [nzValue]="acc.id"
                        [nzLabel]="acc.name"
                      ></nz-option>
                    }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <!-- Category (if not split) -->
              @if (!isSplitActive) {
                <nz-form-item class="mb-3">
                  <nz-form-label>Category</nz-form-label>
                  <nz-form-control>
                    <nz-select
                      formControlName="categoryId"
                      nzPlaceHolder="Select category"
                      nzAllowClear
                    >
                      @for (
                        cat of transactionService.categories();
                        track cat.id
                      ) {
                        <nz-option
                          [nzValue]="cat.id"
                          [nzLabel]="cat.name"
                        ></nz-option>
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
                  <nz-input-number
                    formControlName="amountRupees"
                    [nzMin]="0.01"
                    [nzStep]="10"
                    class="w-full"
                  >
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Date -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker
                    formControlName="date"
                    class="w-full"
                  ></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <!-- Payee -->
              <nz-form-item class="mb-3">
                <nz-form-label>Payee / Merchant</nz-form-label>
                <nz-form-control>
                  <input
                    nz-input
                    formControlName="payee"
                    placeholder="e.g. Swiggy, Amazon, Employer"
                  />
                </nz-form-control>
              </nz-form-item>

              <!-- Visibility -->
              <nz-form-item class="mb-3">
                <nz-form-label>Visibility</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="visibility">
                    <nz-option
                      [nzValue]="1"
                      nzLabel="Household (Shared)"
                    ></nz-option>
                    <nz-option
                      [nzValue]="2"
                      nzLabel="Private (Only Me)"
                    ></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <!-- Notes -->
            <nz-form-item class="mb-3">
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <input
                  nz-input
                  formControlName="notes"
                  placeholder="Optional notes or receipt references"
                />
              </nz-form-control>
            </nz-form-item>

            <!-- Split Toggle -->
            <div
              class="pt-2 border-t border-slate-100 flex items-center justify-between"
            >
              <span class="text-sm font-medium text-slate-700"
                >Split into multiple categories</span
              >
              <nz-switch
                [(ngModel)]="isSplitActive"
                [ngModelOptions]="{ standalone: true }"
              ></nz-switch>
            </div>

            <!-- Split Items Editor -->
            @if (isSplitActive) {
              <div
                class="bg-slate-50 p-3 rounded-xl space-y-2 border border-slate-200"
              >
                <div
                  class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2"
                >
                  Category Splits
                </div>
                @for (split of splitRows; track $index; let i = $index) {
                  <div class="flex items-center gap-2">
                    <nz-select
                      [(ngModel)]="split.categoryId"
                      [ngModelOptions]="{ standalone: true }"
                      class="flex-1"
                      nzPlaceHolder="Select category"
                    >
                      @for (
                        cat of transactionService.categories();
                        track cat.id
                      ) {
                        <nz-option
                          [nzValue]="cat.id"
                          [nzLabel]="cat.name"
                        ></nz-option>
                      }
                    </nz-select>
                    <nz-input-number
                      [(ngModel)]="split.amountRupees"
                      [ngModelOptions]="{ standalone: true }"
                      [nzMin]="0.01"
                      placeholder="₹"
                      class="w-32"
                    >
                    </nz-input-number>
                    @if (splitRows.length > 2) {
                      <button
                        nz-button
                        nzType="text"
                        nzDanger
                        (click)="removeSplitRow(i)"
                      >
                        ✕
                      </button>
                    }
                  </div>
                }
                <button
                  nz-button
                  nzType="dashed"
                  nzBlock
                  (click)="addSplitRow()"
                  class="mt-2 text-xs"
                >
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
          <form
            [formGroup]="transferForm"
            nz-form
            nzLayout="vertical"
            class="space-y-3 pt-2"
          >
            <div
              class="bg-blue-50 border border-blue-200 rounded-lg p-3 text-xs text-blue-800 mb-3"
            >
              💡 Transfers shift funds between your owned accounts. They do
              <strong>not</strong> count as income or expense in household
              totals.
            </div>

            <!-- Source Account -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Source Account (From)</nz-form-label>
              <nz-form-control nzErrorTip="Source account is required">
                <nz-select
                  formControlName="sourceAccountId"
                  nzPlaceHolder="Select source account"
                >
                  @for (acc of accountService.accounts(); track acc.id) {
                    <nz-option
                      [nzValue]="acc.id"
                      [nzLabel]="
                        acc.name +
                        ' (' +
                        (acc.currentBalancePaise | inrCurrency) +
                        ')'
                      "
                    ></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <!-- Destination Account -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Destination Account (To)</nz-form-label>
              <nz-form-control nzErrorTip="Destination account is required">
                <nz-select
                  formControlName="destinationAccountId"
                  nzPlaceHolder="Select destination account"
                >
                  @for (acc of accountService.accounts(); track acc.id) {
                    <nz-option
                      [nzValue]="acc.id"
                      [nzLabel]="
                        acc.name +
                        ' (' +
                        (acc.currentBalancePaise | inrCurrency) +
                        ')'
                      "
                    ></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-3">
              <!-- Amount -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Amount (₹)</nz-form-label>
                <nz-form-control nzErrorTip="Amount must be greater than zero">
                  <nz-input-number
                    formControlName="amountRupees"
                    [nzMin]="0.01"
                    [nzStep]="100"
                    class="w-full"
                  >
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Date -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker
                    formControlName="date"
                    class="w-full"
                  ></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <!-- Notes -->
            <nz-form-item class="mb-3">
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <input
                  nz-input
                  formControlName="notes"
                  placeholder="e.g. Credit card settlement, monthly savings deposit"
                />
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
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
  selectedTypeFilter: number | null = null;
  selectedAccountFilter: string | null = null;
  selectedCategoryFilter: string | null = null;

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
      })
      .subscribe();
  }

  onFilterChange(): void {
    this.page = 1;
    this.loadData();
  }

  onPageChange(p: number): void {
    this.page = p;
    this.loadData();
  }

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
      visibility: 2,
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
        (s) => !s.categoryId || s.amountRupees <= 0,
      );
      if (invalidSplit) {
        this.msg.error(
          "Please select a category and positive amount for each split item.",
        );
        return;
      }

      const totalSplitPaise = this.splitRows.reduce(
        (sum, s) => sum + Math.round(Number(s.amountRupees) * 100),
        0,
      );

      if (totalSplitPaise !== amountPaise) {
        this.msg.error(
          `Split amounts sum to ₹${totalSplitPaise / 100} but total amount is ₹${amountPaise / 100}.`,
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
          this.msg.success("Transaction saved");
          this.isSubmitting = false;
          this.isAddModalVisible = false;
          this.accountService.loadAccounts().subscribe();
        },
        error: (err) => {
          this.msg.error(err.error?.detail || "Failed to save transaction");
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
      this.msg.error("Source and destination accounts must be different");
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
          this.msg.success("Transfer recorded");
          this.isSubmitting = false;
          this.isTransferModalVisible = false;
          this.loadData();
          this.accountService.loadAccounts().subscribe();
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
        this.msg.success("Transaction deleted");
        this.accountService.loadAccounts().subscribe();
      },
      error: () => this.msg.error("Failed to delete transaction"),
    });
  }
}
