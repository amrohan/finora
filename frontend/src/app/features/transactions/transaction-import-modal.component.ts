import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStepsModule } from 'ng-zorro-antd/steps';
import { NzMessageService } from 'ng-zorro-antd/message';
import { InrCurrencyPipe } from '../../shared/pipes/inr-currency.pipe';
import { AccountService } from '../../core/services/account.service';
import {
  TransactionService,
  ParsedFileResult,
  ImportTransactionItem
} from '../../core/services/transaction.service';

export interface PreviewRowItem {
  id: number;
  originalRow: Record<string, string>;
  date: string;
  payee: string;
  amountPaise: number;
  transactionType: number; // 1=Expense, 2=Income
  categoryId?: string;
  categoryName?: string;
  notes?: string;
  isValid: boolean;
  validationError?: string;
  selected: boolean;
}

@Component({
  selector: 'app-transaction-import-modal',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzModalModule,
    NzButtonModule,
    NzSelectModule,
    NzRadioModule,
    NzCheckboxModule,
    NzTableModule,
    NzTagModule,
    NzIconModule,
    NzAlertModule,
    NzSpinModule,
    NzStepsModule,
    InrCurrencyPipe
  ],
  template: `
    <nz-modal
      [(nzVisible)]="isVisible"
      [nzTitle]="'Import Transactions from Excel / CSV'"
      [nzWidth]="880"
      [nzFooter]="null"
      (nzOnCancel)="handleClose()">
      <ng-container *nzModalContent>
        <!-- Steps Header -->
        <nz-steps [nzCurrent]="currentStep" nzSize="small" class="mb-6">
          <nz-step nzTitle="Upload & Account"></nz-step>
          <nz-step nzTitle="Map Columns"></nz-step>
          <nz-step nzTitle="Preview & Import"></nz-step>
        </nz-steps>

        <!-- STEP 0: Upload File & Select Account -->
        @if (currentStep === 0) {
          <div class="space-y-5 py-2">
            <!-- Account & Visibility Row -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block mb-1.5">
                  Target Account <span class="text-rose-500">*</span>
                </label>
                <nz-select [(ngModel)]="targetAccountId" class="w-full" nzPlaceHolder="Select bank or card account">
                  @for (acc of accountService.accounts(); track acc.id) {
                    <nz-option
                      [nzValue]="acc.id"
                      [nzLabel]="acc.name + ' (' + (acc.currentBalancePaise | inrCurrency) + ')'">
                    </nz-option>
                  }
                </nz-select>
              </div>

              <div>
                <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block mb-1.5">
                  Default Record Visibility
                </label>
                <nz-select [(ngModel)]="defaultVisibility" class="w-full">
                  <nz-option [nzValue]="1" nzLabel="Household (Shared)"></nz-option>
                  <nz-option [nzValue]="2" nzLabel="Private (Only Me)"></nz-option>
                </nz-select>
              </div>
            </div>

            <!-- Drag & Drop / File Selector -->
            <div
              (dragover)="onDragOver($event)"
              (drop)="onFileDrop($event)"
              class="border-2 border-dashed border-slate-300 hover:border-emerald-500 rounded-2xl p-8 text-center transition-colors bg-slate-50/50 hover:bg-emerald-50/20 cursor-pointer"
              (click)="fileInput.click()">
              <input
                #fileInput
                type="file"
                accept=".xlsx,.xls,.csv,.tsv,.txt"
                class="hidden"
                (change)="onFileSelected($event)" />

              <div class="flex flex-col items-center justify-center space-y-3">
                <div class="w-14 h-14 rounded-2xl bg-emerald-100 text-emerald-600 flex items-center justify-center text-2xl shadow-xs">
                  <span nz-icon nzType="file-excel"></span>
                </div>
                <div>
                  <h3 class="font-bold text-slate-800 text-base">Click to select or drag and drop statement file</h3>
                  <p class="text-xs text-slate-500 mt-1">
                    Supports Microsoft Excel (.xlsx), CSV, TSV exported from any bank (HDFC, SBI, ICICI, Axis, etc.)
                  </p>
                </div>
                @if (selectedFile) {
                  <div class="inline-flex items-center gap-2 bg-emerald-50 border border-emerald-200 text-emerald-800 px-3 py-1.5 rounded-lg text-xs font-medium">
                    <span nz-icon nzType="check-circle"></span>
                    <span>{{ selectedFile.name }} ({{ formatFileSize(selectedFile.size) }})</span>
                  </div>
                }
              </div>
            </div>

            @if (isParsing()) {
              <div class="text-center py-4">
                <nz-spin nzTip="Inspecting file structure and extracting headers..."></nz-spin>
              </div>
            }

            <div class="flex items-center justify-between pt-3 border-t border-slate-100">
              <span class="text-xs text-slate-400">Max file size: 10 MB</span>
              <button
                nz-button
                nzType="primary"
                [disabled]="!selectedFile || !targetAccountId || isParsing()"
                (click)="goToStep(1)"
                class="bg-emerald-600 hover:bg-emerald-500 border-0 rounded-lg text-sm">
                Next: Map Columns →
              </button>
            </div>
          </div>
        }

        <!-- STEP 1: Column Mapping -->
        @if (currentStep === 1) {
          <div class="space-y-5 py-2">
            <div class="bg-slate-50 p-3 rounded-xl border border-slate-200 text-xs text-slate-600 flex items-center justify-between">
              <div>
                File: <strong class="text-slate-800">{{ parsedResult?.fileName }}</strong>
                ({{ parsedResult?.totalRows }} data rows, {{ parsedResult?.columns?.length }} columns detected)
              </div>
              <button nz-button nzType="text" nzSize="small" (click)="goToStep(0)" class="text-xs text-slate-500 hover:text-slate-800">
                Change File
              </button>
            </div>

            <!-- Required Fields -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <!-- Date Column -->
              <div>
                <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block mb-1.5">
                  Date Column <span class="text-rose-500">*</span>
                </label>
                <nz-select [(ngModel)]="dateCol" class="w-full" nzPlaceHolder="Select column for date">
                  @for (col of parsedResult?.columns; track col) {
                    <nz-option [nzValue]="col" [nzLabel]="col"></nz-option>
                  }
                </nz-select>
              </div>

              <!-- Date Format -->
              <div>
                <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block mb-1.5">
                  Date Format
                </label>
                <nz-select [(ngModel)]="dateFormat" class="w-full">
                  <nz-option nzValue="auto" nzLabel="Auto Detect (DD/MM/YYYY, YYYY-MM-DD, Excel dates)"></nz-option>
                  <nz-option nzValue="DD/MM/YYYY" nzLabel="DD/MM/YYYY (e.g. 15/10/2026)"></nz-option>
                  <nz-option nzValue="YYYY-MM-DD" nzLabel="YYYY-MM-DD (e.g. 2026-10-15)"></nz-option>
                  <nz-option nzValue="MM/DD/YYYY" nzLabel="MM/DD/YYYY (e.g. 10/15/2026)"></nz-option>
                </nz-select>
              </div>
            </div>

            <!-- Payee / Narration Column -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block mb-1.5">
                  Description / Payee Column
                </label>
                <nz-select [(ngModel)]="payeeCol" class="w-full" nzAllowClear nzPlaceHolder="Select description column">
                  @for (col of parsedResult?.columns; track col) {
                    <nz-option [nzValue]="col" [nzLabel]="col"></nz-option>
                  }
                </nz-select>
              </div>

              <!-- Notes / Ref Column -->
              <div>
                <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block mb-1.5">
                  Notes / Reference No. Column (Optional)
                </label>
                <nz-select [(ngModel)]="notesCol" class="w-full" nzAllowClear nzPlaceHolder="Select notes/ref column">
                  @for (col of parsedResult?.columns; track col) {
                    <nz-option [nzValue]="col" [nzLabel]="col"></nz-option>
                  }
                </nz-select>
              </div>
            </div>

            <!-- Amount Mode Selection -->
            <div class="border border-slate-200 rounded-xl p-4 bg-white space-y-4">
              <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block">
                Amount Representation <span class="text-rose-500">*</span>
              </label>

              <nz-radio-group [(ngModel)]="amountMode" class="flex flex-col sm:flex-row gap-4">
                <label nz-radio nzValue="separate" class="text-sm">
                  <strong>Separate Debit & Credit Columns</strong>
                  <span class="block text-xs text-slate-500 font-normal">Withdrawal / Debit = Expense, Deposit / Credit = Income</span>
                </label>
                <label nz-radio nzValue="single" class="text-sm">
                  <strong>Single Amount Column</strong>
                  <span class="block text-xs text-slate-500 font-normal">One column with signs (+/-) or type indicator</span>
                </label>
              </nz-radio-group>

              @if (amountMode === 'separate') {
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
                  <div>
                    <label class="text-xs font-medium text-slate-600 block mb-1">
                      Withdrawal / Debit Column (Expense)
                    </label>
                    <nz-select [(ngModel)]="debitCol" class="w-full" nzPlaceHolder="Select debit column">
                      @for (col of parsedResult?.columns; track col) {
                        <nz-option [nzValue]="col" [nzLabel]="col"></nz-option>
                      }
                    </nz-select>
                  </div>
                  <div>
                    <label class="text-xs font-medium text-slate-600 block mb-1">
                      Deposit / Credit Column (Income)
                    </label>
                    <nz-select [(ngModel)]="creditCol" class="w-full" nzPlaceHolder="Select credit column">
                      @for (col of parsedResult?.columns; track col) {
                        <nz-option [nzValue]="col" [nzLabel]="col"></nz-option>
                      }
                    </nz-select>
                  </div>
                </div>
              } @else {
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
                  <div>
                    <label class="text-xs font-medium text-slate-600 block mb-1">
                      Amount Column
                    </label>
                    <nz-select [(ngModel)]="amountCol" class="w-full" nzPlaceHolder="Select amount column">
                      @for (col of parsedResult?.columns; track col) {
                        <nz-option [nzValue]="col" [nzLabel]="col"></nz-option>
                      }
                    </nz-select>
                  </div>
                  <div>
                    <label class="text-xs font-medium text-slate-600 block mb-1">
                      Amount Sign / Type Logic
                    </label>
                    <nz-select [(ngModel)]="singleAmountTypeMode" class="w-full">
                      <nz-option nzValue="signed" nzLabel="Signed (+ is Income, - is Expense)"></nz-option>
                      <nz-option nzValue="expense" nzLabel="All Rows are Expenses"></nz-option>
                      <nz-option nzValue="income" nzLabel="All Rows are Income"></nz-option>
                      <nz-option nzValue="column" nzLabel="Determined by Type/DR-CR Column"></nz-option>
                    </nz-select>
                  </div>
                </div>

                @if (singleAmountTypeMode === 'column') {
                  <div class="pt-2">
                    <label class="text-xs font-medium text-slate-600 block mb-1">
                      Type / DR-CR Column
                    </label>
                    <nz-select [(ngModel)]="typeCol" class="w-full sm:w-1/2" nzPlaceHolder="Select column with DR / CR / Debit / Credit">
                      @for (col of parsedResult?.columns; track col) {
                        <nz-option [nzValue]="col" [nzLabel]="col"></nz-option>
                      }
                    </nz-select>
                  </div>
                }
              }
            </div>

            <!-- Category & Options -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block mb-1.5">
                  Category Column (Optional)
                </label>
                <nz-select [(ngModel)]="categoryCol" class="w-full" nzAllowClear nzPlaceHolder="Auto-match category from column">
                  @for (col of parsedResult?.columns; track col) {
                    <nz-option [nzValue]="col" [nzLabel]="col"></nz-option>
                  }
                </nz-select>
              </div>

              <div>
                <label class="text-xs font-semibold text-slate-700 uppercase tracking-wider block mb-1.5">
                  Default Fallback Category
                </label>
                <nz-select [(ngModel)]="defaultCategoryId" class="w-full" nzAllowClear nzPlaceHolder="Assign if uncategorized">
                  @for (cat of transactionService.categories(); track cat.id) {
                    <nz-option [nzValue]="cat.id" [nzLabel]="cat.icon ? cat.icon + ' ' + cat.name : cat.name"></nz-option>
                  }
                </nz-select>
              </div>
            </div>

            <!-- Duplicate Skip Option -->
            <div class="flex items-center gap-2 pt-1">
              <label nz-checkbox [(ngModel)]="skipDuplicates" class="text-sm text-slate-700">
                <strong>Skip Duplicate Transactions</strong> (Ignores records matching same date, amount, payee, and account)
              </label>
            </div>

            <!-- Footer navigation -->
            <div class="flex items-center justify-between pt-4 border-t border-slate-100">
              <button nz-button (click)="goToStep(0)">
                ← Back
              </button>
              <button
                nz-button
                nzType="primary"
                [disabled]="!canProceedToPreview()"
                (click)="goToStep(2)"
                class="bg-emerald-600 hover:bg-emerald-500 border-0 rounded-lg text-sm">
                Generate Preview →
              </button>
            </div>
          </div>
        }

        <!-- STEP 2: Preview & Execution -->
        @if (currentStep === 2) {
          <div class="space-y-4 py-2">
            <!-- Summary Metric Cards -->
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <div class="bg-slate-50 p-3 rounded-xl border border-slate-200">
                <span class="text-[10px] font-semibold text-slate-500 uppercase block">Total Rows</span>
                <span class="text-xl font-bold font-mono text-slate-900">{{ previewItems.length }}</span>
              </div>
              <div class="bg-emerald-50 p-3 rounded-xl border border-emerald-200">
                <span class="text-[10px] font-semibold text-emerald-700 uppercase block">Ready to Import</span>
                <span class="text-xl font-bold font-mono text-emerald-700">{{ validSelectedCount() }}</span>
              </div>
              <div class="bg-rose-50 p-3 rounded-xl border border-rose-200">
                <span class="text-[10px] font-semibold text-rose-700 uppercase block">Total Expenses</span>
                <span class="text-base sm:text-lg font-bold font-mono text-rose-700">
                  {{ totalExpensePaise() | inrCurrency }}
                </span>
              </div>
              <div class="bg-blue-50 p-3 rounded-xl border border-blue-200">
                <span class="text-[10px] font-semibold text-blue-700 uppercase block">Total Income</span>
                <span class="text-base sm:text-lg font-bold font-mono text-blue-700">
                  {{ totalIncomePaise() | inrCurrency }}
                </span>
              </div>
            </div>

            <!-- Filter tabs -->
            <div class="flex items-center justify-between gap-2 text-xs">
              <div class="flex items-center gap-1 bg-slate-100 p-1 rounded-lg">
                <button
                  nz-button
                  nzType="text"
                  nzSize="small"
                  (click)="filterMode = 'all'"
                  [class.bg-white]="filterMode === 'all'"
                  [class.shadow-2xs]="filterMode === 'all'"
                  class="rounded-md font-medium text-xs">
                  All ({{ previewItems.length }})
                </button>
                <button
                  nz-button
                  nzType="text"
                  nzSize="small"
                  (click)="filterMode = 'valid'"
                  [class.bg-white]="filterMode === 'valid'"
                  [class.shadow-2xs]="filterMode === 'valid'"
                  class="rounded-md font-medium text-xs text-emerald-700">
                  Valid ({{ validCount() }})
                </button>
                <button
                  nz-button
                  nzType="text"
                  nzSize="small"
                  (click)="filterMode = 'invalid'"
                  [class.bg-white]="filterMode === 'invalid'"
                  [class.shadow-2xs]="filterMode === 'invalid'"
                  class="rounded-md font-medium text-xs text-amber-700">
                  Invalid / Skipped ({{ invalidCount() }})
                </button>
              </div>

              <div class="flex items-center gap-2">
                <button nz-button nzType="text" nzSize="small" (click)="toggleSelectAll(true)" class="text-xs text-slate-600">
                  Select All
                </button>
                <button nz-button nzType="text" nzSize="small" (click)="toggleSelectAll(false)" class="text-xs text-slate-600">
                  Deselect All
                </button>
              </div>
            </div>

            <!-- Preview Table -->
            <div class="max-h-72 overflow-y-auto border border-slate-200 rounded-xl">
              <nz-table
                #previewTable
                [nzData]="filteredPreviewItems"
                [nzFrontPagination]="false"
                [nzShowPagination]="false"
                nzSize="small">
                <thead>
                  <tr class="bg-slate-50 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
                    <th nzWidth="40px" nzAlign="center"></th>
                    <th nzWidth="95px">Date</th>
                    <th>Payee / Narration</th>
                    <th nzWidth="80px">Type</th>
                    <th nzWidth="110px" nzAlign="right">Amount</th>
                    <th>Category</th>
                    <th nzWidth="90px" nzAlign="center">Status</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of previewTable.data; track item.id) {
                    <tr [class.opacity-50]="!item.isValid || !item.selected" class="text-xs hover:bg-slate-50">
                      <td nzAlign="center">
                        <label
                          nz-checkbox
                          [(ngModel)]="item.selected"
                          [nzDisabled]="!item.isValid">
                        </label>
                      </td>
                      <td class="font-mono text-slate-600 whitespace-nowrap">{{ item.date || '—' }}</td>
                      <td class="font-medium text-slate-800">
                        <div class="line-clamp-1" [title]="item.payee">{{ item.payee }}</div>
                        @if (item.notes) {
                          <div class="text-[10px] text-slate-400 font-mono">{{ item.notes }}</div>
                        }
                      </td>
                      <td>
                        @if (item.transactionType === 2) {
                          <nz-tag nzColor="success" class="text-[10px] m-0 rounded">Income</nz-tag>
                        } @else {
                          <nz-tag nzColor="error" class="text-[10px] m-0 rounded">Expense</nz-tag>
                        }
                      </td>
                      <td nzAlign="right" class="font-mono font-bold whitespace-nowrap">
                        <span [ngClass]="item.transactionType === 2 ? 'text-emerald-600' : 'text-rose-600'">
                          {{ item.amountPaise | inrCurrency }}
                        </span>
                      </td>
                      <td class="text-slate-600 text-xs">
                        {{ item.categoryName || 'Uncategorized' }}
                      </td>
                      <td nzAlign="center">
                        @if (item.isValid) {
                          <nz-tag nzColor="default" class="text-[10px] m-0 rounded text-emerald-700 bg-emerald-50 border-emerald-200">
                            Ready
                          </nz-tag>
                        } @else {
                          <nz-tag nzColor="warning" class="text-[10px] m-0 rounded" [title]="item.validationError || 'Invalid row'">
                            Skip
                          </nz-tag>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </nz-table>
            </div>

            <!-- Footer navigation -->
            <div class="flex items-center justify-between pt-4 border-t border-slate-100">
              <button nz-button (click)="goToStep(1)">
                ← Edit Mapping
              </button>

              <button
                nz-button
                nzType="primary"
                [nzLoading]="isSubmitting()"
                [disabled]="validSelectedCount() === 0"
                (click)="executeImport()"
                class="bg-emerald-600 hover:bg-emerald-500 border-0 rounded-lg text-sm px-5">
                <span>Confirm & Import ({{ validSelectedCount() }} Transactions)</span>
              </button>
            </div>
          </div>
        }
      </ng-container>
    </nz-modal>
  `
})
export class TransactionImportModalComponent {
  accountService = inject(AccountService);
  transactionService = inject(TransactionService);
  private msg = inject(NzMessageService);

  @Input() isVisible = false;
  @Output() isVisibleChange = new EventEmitter<boolean>();
  @Output() importCompleted = new EventEmitter<void>();

  currentStep = 0;
  isParsing = signal(false);
  isSubmitting = signal(false);

  selectedFile: File | null = null;
  targetAccountId: string = '';
  defaultVisibility: number = 1;

  parsedResult: ParsedFileResult | null = null;

  // Mapping state
  dateCol = '';
  payeeCol = '';
  notesCol = '';
  categoryCol = '';
  defaultCategoryId: string | null = null;
  amountMode: 'separate' | 'single' = 'separate';
  debitCol = '';
  creditCol = '';
  amountCol = '';
  singleAmountTypeMode: 'signed' | 'expense' | 'income' | 'column' = 'signed';
  typeCol = '';
  dateFormat = 'auto';
  skipDuplicates = true;

  // Preview state
  previewItems: PreviewRowItem[] = [];
  filterMode: 'all' | 'valid' | 'invalid' = 'all';

  get filteredPreviewItems(): PreviewRowItem[] {
    if (this.filterMode === 'valid') return this.previewItems.filter(i => i.isValid);
    if (this.filterMode === 'invalid') return this.previewItems.filter(i => !i.isValid);
    return this.previewItems;
  }

  validCount(): number {
    return this.previewItems.filter(i => i.isValid).length;
  }

  invalidCount(): number {
    return this.previewItems.filter(i => !i.isValid).length;
  }

  validSelectedCount(): number {
    return this.previewItems.filter(i => i.isValid && i.selected).length;
  }

  totalExpensePaise(): number {
    return this.previewItems
      .filter(i => i.isValid && i.selected && i.transactionType === 1)
      .reduce((sum, i) => sum + i.amountPaise, 0);
  }

  totalIncomePaise(): number {
    return this.previewItems
      .filter(i => i.isValid && i.selected && i.transactionType === 2)
      .reduce((sum, i) => sum + i.amountPaise, 0);
  }

  handleClose(): void {
    this.isVisible = false;
    this.isVisibleChange.emit(false);
    this.resetModal();
  }

  resetModal(): void {
    this.currentStep = 0;
    this.selectedFile = null;
    this.parsedResult = null;
    this.previewItems = [];
    this.filterMode = 'all';
  }

  onDragOver(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
  }

  onFileDrop(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    if (e.dataTransfer && e.dataTransfer.files.length > 0) {
      this.processFile(e.dataTransfer.files[0]);
    }
  }

  onFileSelected(e: Event): void {
    const input = e.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.processFile(input.files[0]);
    }
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  processFile(file: File): void {
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
    if (!['.xlsx', '.xls', '.csv', '.tsv', '.txt'].includes(ext)) {
      this.msg.error('Please upload an Excel (.xlsx) or CSV file.');
      return;
    }

    this.selectedFile = file;
    if (!this.targetAccountId && this.accountService.accounts().length > 0) {
      this.targetAccountId = this.accountService.accounts()[0].id;
    }

    this.isParsing.set(true);
    this.transactionService.parseImportFile(file).subscribe({
      next: res => {
        this.parsedResult = res;
        this.isParsing.set(false);
        this.autoMapColumns(res.columns);
        this.msg.success(`Parsed ${res.columns.length} columns and ${res.totalRows} data rows.`);
      },
      error: err => {
        this.isParsing.set(false);
        this.msg.error(err.error?.detail || 'Failed to parse file.');
      }
    });
  }

  autoMapColumns(columns: string[]): void {
    const findCol = (keywords: string[]): string => {
      const match = columns.find(c => {
        const lower = c.toLowerCase().trim();
        return keywords.some(k => lower.includes(k));
      });
      return match || '';
    };

    this.dateCol = findCol(['date', 'txn date', 'transaction date', 'posting date', 'value date']) || columns[0] || '';
    this.payeeCol = findCol(['narration', 'description', 'particular', 'payee', 'merchant', 'details', 'remarks', 'party']) || '';
    this.notesCol = findCol(['ref', 'cheque', 'chq', 'utr', 'notes']) || '';
    this.categoryCol = findCol(['category', 'cat']) || '';

    const debitMatch = findCol(['debit', 'withdrawal', 'dr', 'payment']);
    const creditMatch = findCol(['credit', 'deposit', 'cr']);

    if (debitMatch && creditMatch) {
      this.amountMode = 'separate';
      this.debitCol = debitMatch;
      this.creditCol = creditMatch;
    } else {
      this.amountMode = 'single';
      this.amountCol = findCol(['amount', 'amt', 'total', 'net']) || '';
      this.singleAmountTypeMode = 'signed';
    }
  }

  canProceedToPreview(): boolean {
    if (!this.dateCol) return false;
    if (this.amountMode === 'separate') {
      return !!(this.debitCol || this.creditCol);
    } else {
      return !!this.amountCol;
    }
  }

  goToStep(step: number): void {
    if (step === 2) {
      this.generatePreview();
    }
    this.currentStep = step;
  }

  generatePreview(): void {
    if (!this.parsedResult || !this.parsedResult.allRows) return;

    const categories = this.transactionService.categories();
    const items: PreviewRowItem[] = [];

    this.parsedResult.allRows.forEach((row, idx) => {
      const rawDate = (row[this.dateCol] || '').trim();
      const parsedDate = this.parseDateString(rawDate, this.dateFormat);

      const rawPayee = this.payeeCol ? (row[this.payeeCol] || '').trim() : '';
      const payee = rawPayee || 'Imported Transaction';

      const rawNotes = this.notesCol ? (row[this.notesCol] || '').trim() : '';

      // Determine amount & type
      let amountPaise = 0;
      let transactionType = 1; // 1=Expense, 2=Income
      let isValidAmount = false;

      if (this.amountMode === 'separate') {
        const rawDebit = this.debitCol ? (row[this.debitCol] || '').trim() : '';
        const rawCredit = this.creditCol ? (row[this.creditCol] || '').trim() : '';

        const debitNum = this.parseCleanNumber(rawDebit);
        const creditNum = this.parseCleanNumber(rawCredit);

        if (creditNum > 0) {
          transactionType = 2; // Income
          amountPaise = Math.round(creditNum * 100);
          isValidAmount = true;
        } else if (debitNum > 0) {
          transactionType = 1; // Expense
          amountPaise = Math.round(debitNum * 100);
          isValidAmount = true;
        }
      } else {
        const rawAmt = this.amountCol ? (row[this.amountCol] || '').trim() : '';
        const amtNum = this.parseCleanNumber(rawAmt);

        if (amtNum !== 0) {
          if (this.singleAmountTypeMode === 'income') {
            transactionType = 2;
            amountPaise = Math.round(Math.abs(amtNum) * 100);
            isValidAmount = true;
          } else if (this.singleAmountTypeMode === 'expense') {
            transactionType = 1;
            amountPaise = Math.round(Math.abs(amtNum) * 100);
            isValidAmount = true;
          } else if (this.singleAmountTypeMode === 'column') {
            const typeVal = this.typeCol ? (row[this.typeCol] || '').toLowerCase() : '';
            if (typeVal.includes('cr') || typeVal.includes('credit') || typeVal.includes('deposit')) {
              transactionType = 2;
            } else {
              transactionType = 1;
            }
            amountPaise = Math.round(Math.abs(amtNum) * 100);
            isValidAmount = true;
          } else {
            // Signed: positive is income, negative is expense
            if (amtNum < 0) {
              transactionType = 1; // Expense
              amountPaise = Math.round(Math.abs(amtNum) * 100);
              isValidAmount = true;
            } else {
              transactionType = 2; // Income
              amountPaise = Math.round(amtNum * 100);
              isValidAmount = true;
            }
          }
        }
      }

      // Category matching
      let matchedCategoryId: string | undefined = undefined;
      let matchedCategoryName: string | undefined = undefined;

      const rawCat = this.categoryCol ? (row[this.categoryCol] || '').trim() : '';
      if (rawCat) {
        const matched = categories.find(c => c.name.toLowerCase() === rawCat.toLowerCase());
        if (matched) {
          matchedCategoryId = matched.id;
          matchedCategoryName = matched.name;
        }
      }

      if (!matchedCategoryId && this.defaultCategoryId) {
        matchedCategoryId = this.defaultCategoryId;
        matchedCategoryName = categories.find(c => c.id === this.defaultCategoryId)?.name;
      }

      // Check validity
      let isValid = true;
      let validationError = '';

      if (!parsedDate) {
        isValid = false;
        validationError = 'Missing or unparseable date';
      } else if (!isValidAmount || amountPaise <= 0) {
        isValid = false;
        validationError = 'Missing or zero amount';
      }

      items.push({
        id: idx,
        originalRow: row,
        date: parsedDate,
        payee,
        amountPaise,
        transactionType,
        categoryId: matchedCategoryId,
        categoryName: matchedCategoryName,
        notes: rawNotes,
        isValid,
        validationError,
        selected: isValid
      });
    });

    this.previewItems = items;
  }

  parseCleanNumber(str: string): number {
    if (!str) return 0;
    let s = str.trim();

    // Check for negative in parentheses: (123.45)
    let isNeg = false;
    if (s.startsWith('(') && s.endsWith(')')) {
      isNeg = true;
      s = s.substring(1, s.length - 1);
    }

    // Check for trailing DR / CR
    if (s.endsWith('DR') || s.endsWith('dr')) {
      isNeg = true;
      s = s.substring(0, s.length - 2);
    } else if (s.endsWith('CR') || s.endsWith('cr')) {
      s = s.substring(0, s.length - 2);
    }

    // Strip currency symbols (₹, $, Rs), commas and spaces
    s = s.replace(/[₹$,\sRs.]/gi, '');
    const num = parseFloat(s);
    if (isNaN(num)) return 0;
    return isNeg ? -Math.abs(num) : num;
  }

  parseDateString(str: string, format: string): string {
    if (!str) return '';
    const s = str.trim();

    // 1. Check if Excel numeric serial date (e.g. 45596)
    if (/^\d{5}(\.\d+)?$/.test(s)) {
      const serial = parseFloat(s);
      // Excel epoch begins Dec 30 1899
      const utcDays = Math.floor(serial - 25569);
      const date = new Date(utcDays * 86400 * 1000);
      return date.toISOString().split('T')[0];
    }

    // 2. Format: YYYY-MM-DD or YYYY/MM/DD
    if (/^\d{4}[-/]\d{1,2}[-/]\d{1,2}$/.test(s)) {
      const parts = s.split(/[-/]/);
      return `${parts[0]}-${parts[1].padStart(2, '0')}-${parts[2].padStart(2, '0')}`;
    }

    // 3. Format: DD-MMM-YYYY (e.g. 15-Oct-2026 or 15 Oct 2026)
    const monthNames: Record<string, string> = {
      jan: '01', feb: '02', mar: '03', apr: '04', may: '05', jun: '06',
      jul: '07', aug: '08', sep: '09', oct: '10', nov: '11', dec: '12'
    };
    const namedMonthMatch = s.match(/^(\d{1,2})[- ]([A-Za-z]{3})[- ](\d{4})$/);
    if (namedMonthMatch) {
      const day = namedMonthMatch[1].padStart(2, '0');
      const mon = monthNames[namedMonthMatch[2].toLowerCase()] || '01';
      const year = namedMonthMatch[3];
      return `${year}-${mon}-${day}`;
    }

    // 4. Format: DD/MM/YYYY or DD-MM-YYYY
    const slashParts = s.split(/[-/.]/);
    if (slashParts.length === 3) {
      const p0 = parseInt(slashParts[0], 10);
      const p1 = parseInt(slashParts[1], 10);
      const p2 = parseInt(slashParts[2], 10);

      // If Year is first: YYYY/MM/DD
      if (p0 > 1000) {
        return `${p0}-${String(p1).padStart(2, '0')}-${String(p2).padStart(2, '0')}`;
      }

      // If Year is last: DD/MM/YYYY or MM/DD/YYYY
      const year = p2 < 100 ? (2000 + p2) : p2;

      if (format === 'MM/DD/YYYY') {
        return `${year}-${String(p0).padStart(2, '0')}-${String(p1).padStart(2, '0')}`;
      }

      // Default Indian format: DD/MM/YYYY
      return `${year}-${String(p1).padStart(2, '0')}-${String(p0).padStart(2, '0')}`;
    }

    // 5. Native JS Date parse fallback
    const parsed = new Date(s);
    if (!isNaN(parsed.getTime())) {
      return parsed.toISOString().split('T')[0];
    }

    return '';
  }

  toggleSelectAll(select: boolean): void {
    this.previewItems.forEach(i => {
      if (i.isValid) i.selected = select;
    });
  }

  executeImport(): void {
    const selected = this.previewItems.filter(i => i.isValid && i.selected);
    if (selected.length === 0) {
      this.msg.warning('No valid transactions selected for import.');
      return;
    }

    const payload: ImportTransactionItem[] = selected.map(i => ({
      date: i.date,
      payee: i.payee,
      amountPaise: i.amountPaise,
      transactionType: i.transactionType,
      categoryId: i.categoryId,
      notes: i.notes,
      visibility: this.defaultVisibility
    }));

    this.isSubmitting.set(true);
    this.transactionService.executeImport({
      accountId: this.targetAccountId,
      transactions: payload,
      skipDuplicates: this.skipDuplicates
    }).subscribe({
      next: res => {
        this.isSubmitting.set(false);
        this.msg.success(
          `Successfully imported ${res.importedCount} transactions! (${res.skippedCount} duplicates skipped)`
        );
        this.accountService.loadAccounts().subscribe();
        this.importCompleted.emit();
        this.handleClose();
      },
      error: err => {
        this.isSubmitting.set(false);
        this.msg.error(err.error?.detail || 'Import failed. Please verify your data.');
      }
    });
  }
}

