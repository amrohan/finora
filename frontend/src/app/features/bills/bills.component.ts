import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { InrCurrencyPipe } from '../../shared/pipes/inr-currency.pipe';
import { BillService, RecurringBill, BillOccurrence, BillFrequency } from '../../core/services/bill.service';
import { AccountService } from '../../core/services/account.service';
import { TransactionService, Category } from '../../core/services/transaction.service';

@Component({
  selector: 'app-bills',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzTableModule,
    NzTabsModule,
    NzTagModule,
    NzPopconfirmModule,
    NzAlertModule,
    NzEmptyModule,
    InrCurrencyPipe
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Recurring Bills & Subscriptions</h1>
          <p class="text-sm text-slate-500 mt-1">Track utility bills, SIP commitments, insurance premiums, and dues</p>
        </div>

        <button
          nz-button
          nzType="primary"
          (click)="openAddBillModal()"
          class="rounded-lg text-sm font-medium bg-emerald-600 hover:bg-emerald-500 border-0 shadow-xs flex items-center gap-1.5">
          <span>➕ Add Recurring Bill</span>
        </button>
      </div>

      <!-- Overdue Alert Indicator (Criterion #5) -->
      @if (billService.overdueOccurrences().length > 0) {
        <div class="bg-rose-50 border border-rose-200 rounded-xl p-4 flex items-center justify-between shadow-xs">
          <div class="flex items-center gap-3">
            <span class="text-2xl">⚠️</span>
            <div>
              <span class="font-bold text-rose-900 text-sm block">
                {{ billService.overdueOccurrences().length }} Overdue Payment{{ billService.overdueOccurrences().length > 1 ? 's' : '' }} Requiring Action
              </span>
              <span class="text-xs text-rose-700">Immediate settlement needed to avoid penalties or service disruption</span>
            </div>
          </div>
          <nz-tag nzColor="error" class="rounded font-bold px-2 py-0.5 text-xs">
            Overdue Dues
          </nz-tag>
        </div>
      }

      <!-- Main Tabs View -->
      <nz-card class="rounded-xl border border-slate-200/80 shadow-xs overflow-hidden">
        <nz-tabset>
          <!-- Tab 1: Upcoming Schedule -->
          <nz-tab [nzTitle]="scheduleTabTitle">
            <ng-template #scheduleTabTitle>
              <span>📅 Upcoming Schedule (60 Days)</span>
              @if (billService.overdueOccurrences().length > 0) {
                <nz-tag nzColor="error" class="rounded text-[10px] ml-1.5">{{ billService.overdueOccurrences().length }}</nz-tag>
              }
            </ng-template>

            <nz-table
              #occTable
              [nzData]="billService.occurrences()"
              [nzBordered]="false"
              nzSize="middle"
              [nzLoading]="billService.loading()"
              class="mt-2">
              <thead>
                <tr class="bg-slate-50 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                  <th>Due Date</th>
                  <th>Bill Name</th>
                  <th>Status</th>
                  <th nzAlign="right">Expected Amount</th>
                  <th nzAlign="center" nzWidth="120px">Action</th>
                </tr>
              </thead>
              <tbody>
                @for (occ of occTable.data; track occ.id) {
                  <tr class="hover:bg-slate-50/60 transition-colors">
                    <td class="font-mono text-xs whitespace-nowrap" [ngClass]="occ.status === 3 ? 'text-rose-600 font-bold' : 'text-slate-600'">
                      {{ occ.dueDate }}
                    </td>
                    <td>
                      <span class="font-medium text-slate-900 text-sm block">{{ occ.billName }}</span>
                      @if (occ.paidAt) {
                        <span class="text-[11px] text-slate-400 font-mono">Paid at {{ occ.paidAt | date:'mediumDate' }}</span>
                      }
                    </td>
                    <td>
                      @switch (occ.status) {
                        @case (1) {
                          <nz-tag nzColor="blue" class="rounded text-xs">Upcoming</nz-tag>
                        }
                        @case (2) {
                          <nz-tag nzColor="success" class="rounded text-xs">✓ Paid</nz-tag>
                        }
                        @case (3) {
                          <nz-tag nzColor="error" class="rounded text-xs font-bold">⚠️ Overdue</nz-tag>
                        }
                        @case (4) {
                          <nz-tag nzColor="default" class="rounded text-xs">Skipped</nz-tag>
                        }
                      }
                    </td>
                    <td nzAlign="right">
                      <span class="font-mono font-bold text-sm tracking-tight text-slate-900">
                        {{ occ.amountPaise | inrCurrency }}
                      </span>
                    </td>
                    <td nzAlign="center">
                      @if (occ.status !== 2) {
                        <button
                          nz-button
                          nzType="primary"
                          nzSize="small"
                          (click)="openPayModal(occ)"
                          class="rounded-md text-xs bg-emerald-600 hover:bg-emerald-500 border-0 font-medium">
                          Pay Now
                        </button>
                      } @else {
                        <span class="text-xs text-slate-400 font-medium">Completed</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </nz-table>
          </nz-tab>

          <!-- Tab 2: Recurring Rules -->
          <nz-tab nzTitle="⚙️ Recurring Rules">
            <nz-table
              #ruleTable
              [nzData]="billService.bills()"
              [nzBordered]="false"
              nzSize="middle"
              [nzLoading]="billService.loading()"
              class="mt-2">
              <thead>
                <tr class="bg-slate-50 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                  <th>Rule Name</th>
                  <th>Frequency</th>
                  <th>Next Due Date</th>
                  <th nzAlign="right">Amount</th>
                  <th nzAlign="center" nzWidth="80px">Action</th>
                </tr>
              </thead>
              <tbody>
                @for (bill of ruleTable.data; track bill.id) {
                  <tr>
                    <td class="font-medium text-slate-900 text-sm">{{ bill.name }}</td>
                    <td>
                      <nz-tag class="rounded text-xs">{{ getFrequencyName(bill.frequency) }}</nz-tag>
                    </td>
                    <td class="font-mono text-xs text-slate-600">{{ bill.nextDueDate }}</td>
                    <td nzAlign="right">
                      <span class="font-mono font-bold text-sm text-slate-900">
                        {{ bill.expectedAmountPaise | inrCurrency }}
                      </span>
                    </td>
                    <td nzAlign="center">
                      <button
                        nz-button
                        nzType="text"
                        nzDanger
                        nz-popconfirm
                        nzPopconfirmTitle="Delete this recurring bill schedule?"
                        (nzOnConfirm)="deleteBill(bill.id)"
                        class="text-xs p-1 text-slate-400 hover:text-rose-600">
                        🗑️
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </nz-table>
          </nz-tab>
        </nz-tabset>
      </nz-card>

      <!-- Add Recurring Bill Modal -->
      <nz-modal
        [(nzVisible)]="isAddModalVisible"
        nzTitle="Schedule Recurring Bill"
        [nzOkText]="'Create Schedule'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closeAddModal()"
        (nzOnOk)="submitAddBill()">
        <ng-container *nzModalContent>
          <form [formGroup]="billForm" nz-form nzLayout="vertical" class="space-y-3 pt-2">
            <!-- Name -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Bill / Commitment Name</nz-form-label>
              <nz-form-control nzErrorTip="Name is required">
                <input nz-input formControlName="name" placeholder="e.g. Jio Fiber, House Rent, Term Insurance" />
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-3">
              <!-- Expected Amount -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Expected Amount (₹)</nz-form-label>
                <nz-form-control nzErrorTip="Amount must be positive">
                  <nz-input-number
                    formControlName="amountRupees"
                    [nzMin]="1"
                    [nzStep]="100"
                    class="w-full">
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Frequency -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Frequency</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="frequency">
                    <nz-option [nzValue]="1" nzLabel="Weekly"></nz-option>
                    <nz-option [nzValue]="2" nzLabel="Monthly"></nz-option>
                    <nz-option [nzValue]="3" nzLabel="Quarterly"></nz-option>
                    <nz-option [nzValue]="4" nzLabel="Yearly"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <!-- Category -->
              <nz-form-item class="mb-3">
                <nz-form-label>Default Category</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="categoryId" nzPlaceHolder="Select category" nzAllowClear>
                    @for (cat of transactionService.categories(); track cat.id) {
                      <nz-option [nzValue]="cat.id" [nzLabel]="cat.icon ? cat.icon + ' ' + cat.name : cat.name"></nz-option>
                    }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <!-- Next Due Date -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>First Due Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="nextDueDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Pay Bill Occurrence Modal (Criterion #4) -->
      <nz-modal
        [(nzVisible)]="isPayModalVisible"
        [nzTitle]="'Pay Bill: ' + (selectedOccurrence?.billName || '')"
        [nzOkText]="'Confirm & Record Expense'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closePayModal()"
        (nzOnOk)="submitPayOccurrence()">
        <ng-container *nzModalContent>
          <div class="bg-emerald-50 border border-emerald-200 rounded-lg p-3 text-xs text-emerald-800 mb-3">
            💡 Marking this occurrence as paid will automatically record an expense transaction of 
            <strong>{{ (selectedOccurrence?.amountPaise || 0) | inrCurrency }}</strong> and deduct it from your chosen account.
          </div>

          <form [formGroup]="payForm" nz-form nzLayout="vertical" class="space-y-3">
            <!-- Account -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Payment Account</nz-form-label>
              <nz-form-control nzErrorTip="Account is required">
                <nz-select formControlName="accountId" nzPlaceHolder="Select account to pay from">
                  @for (acc of accountService.accounts(); track acc.id) {
                    <nz-option [nzValue]="acc.id" [nzLabel]="acc.name + ' (' + (acc.currentBalancePaise | inrCurrency) + ')'"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <!-- Payment Date -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Payment Date</nz-form-label>
              <nz-form-control>
                <nz-date-picker formControlName="paymentDate" class="w-full"></nz-date-picker>
              </nz-form-control>
            </nz-form-item>

            <!-- Notes -->
            <nz-form-item class="mb-3">
              <nz-form-label>Notes / Reference</nz-form-label>
              <nz-form-control>
                <input nz-input formControlName="notes" placeholder="e.g. UTR / Transaction ID / Paid via PhonePe" />
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
    </div>
  `
})
export class BillsComponent implements OnInit {
  billService = inject(BillService);
  accountService = inject(AccountService);
  transactionService = inject(TransactionService);
  private fb = inject(FormBuilder);
  private msg = inject(NzMessageService);

  isAddModalVisible = false;
  isPayModalVisible = false;
  isSubmitting = false;

  selectedOccurrence: BillOccurrence | null = null;

  billForm = this.fb.group({
    name: ['', [Validators.required]],
    amountRupees: [0, [Validators.required, Validators.min(1)]],
    frequency: [2, [Validators.required]], // Monthly default
    nextDueDate: [new Date(), [Validators.required]],
    categoryId: ['']
  });

  payForm = this.fb.group({
    accountId: ['', [Validators.required]],
    paymentDate: [new Date(), [Validators.required]],
    notes: ['']
  });

  ngOnInit(): void {
    this.accountService.loadAccounts().subscribe();
    this.transactionService.loadCategories().subscribe();
    this.billService.loadBills().subscribe();
    this.billService.loadOccurrences().subscribe();
  }

  openAddBillModal(): void {
    this.billForm.reset({
      name: '',
      amountRupees: 1000,
      frequency: 2,
      nextDueDate: new Date(),
      categoryId: ''
    });
    this.isAddModalVisible = true;
  }

  closeAddModal(): void {
    this.isAddModalVisible = false;
  }

  submitAddBill(): void {
    if (this.billForm.invalid) {
      Object.values(this.billForm.controls).forEach(c => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.billForm.value;
    const expectedAmountPaise = Math.round(Number(val.amountRupees) * 100);
    const dateVal = val.nextDueDate ? new Date(val.nextDueDate) : new Date();
    const formattedDate = dateVal.toISOString().split('T')[0];

    this.isSubmitting = true;
    this.billService.createBill({
      name: val.name!,
      expectedAmountPaise,
      frequency: Number(val.frequency) as BillFrequency,
      nextDueDate: formattedDate,
      categoryId: val.categoryId || undefined
    }).subscribe({
      next: () => {
        this.msg.success('Recurring bill schedule created');
        this.isSubmitting = false;
        this.isAddModalVisible = false;
      },
      error: err => {
        this.msg.error(err.error?.detail || 'Failed to schedule bill');
        this.isSubmitting = false;
      }
    });
  }

  openPayModal(occ: BillOccurrence): void {
    this.selectedOccurrence = occ;
    this.payForm.reset({
      accountId: this.accountService.accounts()[0]?.id || '',
      paymentDate: new Date(),
      notes: `Bill payment for ${occ.billName}`
    });
    this.isPayModalVisible = true;
  }

  closePayModal(): void {
    this.isPayModalVisible = false;
    this.selectedOccurrence = null;
  }

  submitPayOccurrence(): void {
    if (this.payForm.invalid || !this.selectedOccurrence) {
      Object.values(this.payForm.controls).forEach(c => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.payForm.value;
    const dateVal = val.paymentDate ? new Date(val.paymentDate) : new Date();
    const formattedDate = dateVal.toISOString().split('T')[0];

    this.isSubmitting = true;
    this.billService.payOccurrence(this.selectedOccurrence.id, {
      accountId: val.accountId!,
      paymentDate: formattedDate,
      notes: val.notes?.trim() || undefined
    }).subscribe({
      next: () => {
        this.msg.success('Bill paid & expense recorded atomically');
        this.isSubmitting = false;
        this.isPayModalVisible = false;
        this.accountService.loadAccounts().subscribe();
      },
      error: err => {
        this.msg.error(err.error?.detail || 'Failed to process payment');
        this.isSubmitting = false;
      }
    });
  }

  deleteBill(id: string): void {
    this.billService.deleteBill(id).subscribe({
      next: () => this.msg.success('Recurring bill removed'),
      error: () => this.msg.error('Failed to remove bill')
    });
  }

  getFrequencyName(freq: number): string {
    switch (freq) {
      case 1: return 'Weekly';
      case 2: return 'Monthly';
      case 3: return 'Quarterly';
      case 4: return 'Yearly';
      default: return 'Monthly';
    }
  }
}

