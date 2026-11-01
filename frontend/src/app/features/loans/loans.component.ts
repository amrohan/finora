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
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { InrCurrencyPipe } from '../../shared/pipes/inr-currency.pipe';
import { LoanService, Loan, AmortizationItem } from '../../core/services/loan.service';
import { AccountService } from '../../core/services/account.service';

@Component({
  selector: 'app-loans',
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
    NzProgressModule,
    NzTagModule,
    NzTableModule,
    NzPopconfirmModule,
    NzEmptyModule,
    InrCurrencyPipe
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Loans & Debt Management</h1>
          <p class="text-sm text-slate-500 mt-1">Track mortgages, car loans, reducing-balance EMIs, and debt elimination</p>
        </div>

        <button
          nz-button
          nzType="primary"
          (click)="openAddLoanModal()"
          class="rounded-lg text-sm font-medium bg-emerald-600 hover:bg-emerald-500 border-0 shadow-xs flex items-center gap-1.5">
          <span>➕ Add Loan</span>
        </button>
      </div>

      <!-- Total Debt Banner -->
      <div class="bg-gradient-to-r from-slate-900 to-slate-800 rounded-2xl p-6 text-white shadow-sm flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider block">Total Outstanding Debt</span>
          <span class="text-3xl font-bold font-mono tracking-tight text-rose-400 mt-1 block">
            {{ loanService.totalDebtPaise() | inrCurrency }}
          </span>
        </div>
        <div class="flex items-center gap-6 text-sm text-slate-300">
          <div>
            <span class="text-slate-400 text-xs block">Active Loans</span>
            <span class="font-bold text-lg text-white font-mono">{{ loanService.loans().length }}</span>
          </div>
        </div>
      </div>

      <!-- Loan Cards Grid -->
      @if (loanService.loans().length === 0) {
        <nz-card class="rounded-xl border border-slate-200/80 text-center py-12">
          <nz-empty
            nzNotFoundImage="simple"
            nzNotFoundContent="No active loans recorded. Add mortgages, vehicle loans, or personal debt to track EMIs.">
            <button
              nz-button
              nzType="primary"
              (click)="openAddLoanModal()"
              class="mt-3 bg-emerald-600 hover:bg-emerald-500 border-0 rounded-lg text-sm">
              Add First Loan
            </button>
          </nz-empty>
        </nz-card>
      } @else {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          @for (loan of loanService.loans(); track loan.id) {
            <nz-card class="rounded-xl border border-slate-200/80 shadow-xs hover:shadow-md transition-shadow">
              <!-- Card Header -->
              <div class="flex items-start justify-between gap-2 mb-3">
                <div>
                  <h3 class="font-bold text-slate-900 text-base leading-tight">{{ loan.name }}</h3>
                  <div class="flex items-center gap-2 mt-1">
                    <nz-tag nzColor="blue" class="rounded text-[11px] font-medium font-mono">
                      {{ loan.annualInterestRate }}% p.a.
                    </nz-tag>
                    <span class="text-xs text-slate-400">{{ loan.tenureMonths }} mos tenure</span>
                  </div>
                </div>

                <div class="text-right">
                  <span class="text-[10px] text-slate-400 font-semibold uppercase tracking-wider block">Monthly EMI</span>
                  <span class="font-mono font-bold text-emerald-600 text-sm">
                    {{ loan.monthlyEmiPaise | inrCurrency }}
                  </span>
                </div>
              </div>

              <!-- Principal Balances -->
              <div class="space-y-2 mt-4 pt-3 border-t border-slate-100">
                <div class="flex items-baseline justify-between text-xs">
                  <span class="text-slate-500">Remaining Balance:</span>
                  <span class="font-mono font-bold text-slate-900 text-sm">
                    {{ loan.currentPrincipalPaise | inrCurrency }}
                    <span class="text-slate-400 font-normal">/ {{ loan.originalPrincipalPaise | inrCurrency }}</span>
                  </span>
                </div>

                <!-- Payoff Progress Bar -->
                @let paidPercent = getPaidPercentage(loan);
                <nz-progress
                  [nzPercent]="paidPercent"
                  [nzStatus]="paidPercent === 100 ? 'success' : 'active'"
                  [nzStrokeColor]="'#10b981'">
                </nz-progress>
                <div class="flex items-center justify-between text-[11px] text-slate-400">
                  <span>{{ paidPercent }}% Paid Off</span>
                  <span>Started {{ loan.startDate }}</span>
                </div>
              </div>

              <!-- Actions -->
              <div class="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between gap-2">
                <button
                  nz-button
                  nzType="primary"
                  nzSize="small"
                  (click)="openPaymentModal(loan)"
                  class="rounded-md text-xs bg-emerald-600 hover:bg-emerald-500 border-0 font-medium">
                  Record Payment
                </button>

                <div class="flex items-center gap-1">
                  <button
                    nz-button
                    nzType="default"
                    nzSize="small"
                    (click)="openAmortizationModal(loan)"
                    class="rounded-md text-xs">
                    Amortization
                  </button>
                  <button
                    nz-button
                    nzType="text"
                    nzDanger
                    nz-popconfirm
                    nzPopconfirmTitle="Delete this loan?"
                    (nzOnConfirm)="deleteLoan(loan.id)"
                    class="text-xs p-1 text-slate-400 hover:text-rose-600">
                    ✕
                  </button>
                </div>
              </div>
            </nz-card>
          }
        </div>
      }

      <!-- Add Loan Modal -->
      <nz-modal
        [(nzVisible)]="isAddModalVisible"
        nzTitle="Add New Loan"
        [nzOkText]="'Save Loan'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closeAddModal()"
        (nzOnOk)="submitAddLoan()">
        <ng-container *nzModalContent>
          <form [formGroup]="loanForm" nz-form nzLayout="vertical" class="space-y-3 pt-2">
            <!-- Name -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Loan Name</nz-form-label>
              <nz-form-control nzErrorTip="Loan name is required">
                <input nz-input formControlName="name" placeholder="e.g. SBI Home Loan, HDFC Car Loan" />
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-3">
              <!-- Principal (₹) -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Original Principal (₹)</nz-form-label>
                <nz-form-control nzErrorTip="Amount must be positive">
                  <nz-input-number
                    formControlName="principalRupees"
                    [nzMin]="1"
                    [nzStep]="50000"
                    class="w-full">
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Interest Rate % -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Annual Interest Rate (%)</nz-form-label>
                <nz-form-control nzErrorTip="Rate is required">
                  <nz-input-number
                    formControlName="interestRate"
                    [nzMin]="0"
                    [nzMax]="100"
                    [nzStep]="0.1"
                    class="w-full">
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <!-- Tenure Months -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Tenure (Months)</nz-form-label>
                <nz-form-control nzErrorTip="Tenure must be at least 1 month">
                  <nz-input-number
                    formControlName="tenureMonths"
                    [nzMin]="1"
                    [nzMax]="480"
                    [nzStep]="12"
                    class="w-full">
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Start Date -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Start Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="startDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Record Payment Modal (Criterion #2) -->
      <nz-modal
        [(nzVisible)]="isPaymentModalVisible"
        [nzTitle]="'Record Loan Payment: ' + (selectedLoan?.name || '')"
        [nzOkText]="'Process Repayment'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closePaymentModal()"
        (nzOnOk)="submitPayment()">
        <ng-container *nzModalContent>
          <div class="bg-blue-50 border border-blue-200 rounded-lg p-3 text-xs text-blue-800 mb-3">
            💡 <strong>Reducing Balance Invariant:</strong> The payment is automatically split into principal reduction and interest expenditure. Only the interest portion counts as a finance expense on your household totals.
          </div>

          <form [formGroup]="paymentForm" nz-form nzLayout="vertical" class="space-y-3">
            <!-- Account -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Paying Bank Account</nz-form-label>
              <nz-form-control nzErrorTip="Account is required">
                <nz-select formControlName="paymentAccountId" nzPlaceHolder="Select account">
                  @for (acc of accountService.accounts(); track acc.id) {
                    <nz-option [nzValue]="acc.id" [nzLabel]="acc.name + ' (' + (acc.currentBalancePaise | inrCurrency) + ')'"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-3">
              <!-- Payment Amount (₹) -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Payment Amount (₹)</nz-form-label>
                <nz-form-control nzErrorTip="Amount must be positive">
                  <nz-input-number
                    formControlName="amountRupees"
                    [nzMin]="1"
                    [nzStep]="1000"
                    class="w-full">
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Payment Date -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Payment Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="paymentDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <!-- Notes -->
            <nz-form-item class="mb-3">
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <input nz-input formControlName="notes" placeholder="e.g. Regular EMI / Extra prepayment" />
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Amortization Schedule Modal (Criterion #1 & #4) -->
      <nz-modal
        [(nzVisible)]="isAmortModalVisible"
        [nzTitle]="'Amortization Schedule: ' + (selectedLoan?.name || '')"
        [nzWidth]="750"
        [nzFooter]="null"
        (nzOnCancel)="closeAmortModal()">
        <ng-container *nzModalContent>
          <nz-table
            #amortTable
            [nzData]="amortizationSchedule"
            [nzPageSize]="12"
            nzSize="small"
            class="mt-2">
            <thead>
              <tr class="bg-slate-50 text-xs font-semibold text-slate-500 uppercase tracking-wider">
                <th>#</th>
                <th>Date</th>
                <th nzAlign="right">EMI</th>
                <th nzAlign="right">Principal</th>
                <th nzAlign="right">Interest</th>
                <th nzAlign="right">Remaining Balance</th>
              </tr>
            </thead>
            <tbody>
              @for (item of amortTable.data; track item.monthIndex) {
                <tr class="text-xs hover:bg-slate-50">
                  <td class="text-slate-400 font-mono">{{ item.monthIndex }}</td>
                  <td class="font-mono text-slate-600">{{ item.paymentDate }}</td>
                  <td nzAlign="right" class="font-mono font-medium text-slate-800">{{ item.emiPaise | inrCurrency }}</td>
                  <td nzAlign="right" class="font-mono text-emerald-600 font-medium">{{ item.principalPaise | inrCurrency }}</td>
                  <td nzAlign="right" class="font-mono text-rose-500 font-medium">{{ item.interestPaise | inrCurrency }}</td>
                  <td nzAlign="right" class="font-mono font-bold text-slate-900">{{ item.remainingPrincipalPaise | inrCurrency }}</td>
                </tr>
              }
            </tbody>
          </nz-table>
        </ng-container>
      </nz-modal>
    </div>
  `
})
export class LoansComponent implements OnInit {
  loanService = inject(LoanService);
  accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private msg = inject(NzMessageService);

  isAddModalVisible = false;
  isPaymentModalVisible = false;
  isAmortModalVisible = false;
  isSubmitting = false;

  selectedLoan: Loan | null = null;
  amortizationSchedule: AmortizationItem[] = [];

  loanForm = this.fb.group({
    name: ['', [Validators.required]],
    principalRupees: [500000, [Validators.required, Validators.min(1)]],
    interestRate: [8.5, [Validators.required, Validators.min(0)]],
    tenureMonths: [60, [Validators.required, Validators.min(1)]],
    startDate: [new Date(), [Validators.required]]
  });

  paymentForm = this.fb.group({
    paymentAccountId: ['', [Validators.required]],
    amountRupees: [0, [Validators.required, Validators.min(1)]],
    paymentDate: [new Date(), [Validators.required]],
    notes: ['']
  });

  ngOnInit(): void {
    this.accountService.loadAccounts().subscribe();
    this.loanService.loadLoans().subscribe();
  }

  getPaidPercentage(loan: Loan): number {
    if (loan.originalPrincipalPaise <= 0) return 0;
    const paid = loan.originalPrincipalPaise - loan.currentPrincipalPaise;
    return Math.min(100, Math.max(0, Math.round((paid / loan.originalPrincipalPaise) * 100)));
  }

  openAddLoanModal(): void {
    this.loanForm.reset({
      name: '',
      principalRupees: 500000,
      interestRate: 8.5,
      tenureMonths: 60,
      startDate: new Date()
    });
    this.isAddModalVisible = true;
  }

  closeAddModal(): void {
    this.isAddModalVisible = false;
  }

  submitAddLoan(): void {
    if (this.loanForm.invalid) {
      Object.values(this.loanForm.controls).forEach(c => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.loanForm.value;
    const originalPrincipalPaise = Math.round(Number(val.principalRupees) * 100);
    const dateVal = val.startDate ? new Date(val.startDate) : new Date();
    const formattedDate = dateVal.toISOString().split('T')[0];

    this.isSubmitting = true;
    this.loanService.createLoan({
      name: val.name!,
      originalPrincipalPaise,
      annualInterestRate: Number(val.interestRate),
      tenureMonths: Number(val.tenureMonths),
      startDate: formattedDate
    }).subscribe({
      next: () => {
        this.msg.success('Loan and liability account created successfully');
        this.isSubmitting = false;
        this.isAddModalVisible = false;
        this.accountService.loadAccounts().subscribe();
      },
      error: err => {
        this.msg.error(err.error?.detail || 'Failed to create loan');
        this.isSubmitting = false;
      }
    });
  }

  openPaymentModal(loan: Loan): void {
    this.selectedLoan = loan;
    this.paymentForm.reset({
      paymentAccountId: this.accountService.accounts()[0]?.id || '',
      amountRupees: loan.monthlyEmiPaise / 100,
      paymentDate: new Date(),
      notes: `EMI Repayment: ${loan.name}`
    });
    this.isPaymentModalVisible = true;
  }

  closePaymentModal(): void {
    this.isPaymentModalVisible = false;
    this.selectedLoan = null;
  }

  submitPayment(): void {
    if (this.paymentForm.invalid || !this.selectedLoan) {
      Object.values(this.paymentForm.controls).forEach(c => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.paymentForm.value;
    const paymentAmountPaise = Math.round(Number(val.amountRupees) * 100);
    const dateVal = val.paymentDate ? new Date(val.paymentDate) : new Date();
    const formattedDate = dateVal.toISOString().split('T')[0];

    this.isSubmitting = true;
    this.loanService.recordPayment(this.selectedLoan.id, {
      paymentAccountId: val.paymentAccountId!,
      paymentAmountPaise,
      paymentDate: formattedDate,
      notes: val.notes?.trim() || undefined
    }).subscribe({
      next: () => {
        this.msg.success('Payment recorded and principal reduced');
        this.isSubmitting = false;
        this.isPaymentModalVisible = false;
        this.accountService.loadAccounts().subscribe();
      },
      error: err => {
        this.msg.error(err.error?.detail || 'Failed to record payment');
        this.isSubmitting = false;
      }
    });
  }

  openAmortizationModal(loan: Loan): void {
    this.selectedLoan = loan;
    this.loanService.getAmortization(loan.id).subscribe(schedule => {
      this.amortizationSchedule = schedule;
      this.isAmortModalVisible = true;
    });
  }

  closeAmortModal(): void {
    this.isAmortModalVisible = false;
    this.selectedLoan = null;
    this.amortizationSchedule = [];
  }

  deleteLoan(id: string): void {
    this.loanService.deleteLoan(id).subscribe({
      next: () => this.msg.success('Loan deleted'),
      error: () => this.msg.error('Failed to delete loan')
    });
  }
}

