import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { InrCurrencyPipe } from '../../shared/pipes/inr-currency.pipe';
import { BudgetService, Budget } from '../../core/services/budget.service';
import { TransactionService, Category } from '../../core/services/transaction.service';

@Component({
  selector: 'app-budgets',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzModalModule,
    NzFormModule,
    NzSelectModule,
    NzInputNumberModule,
    NzSwitchModule,
    NzProgressModule,
    NzTagModule,
    NzPopconfirmModule,
    NzEmptyModule,
    InrCurrencyPipe
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto">
      <!-- Header & Month Selector -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Monthly Budgets</h1>
          <p class="text-sm text-slate-500 mt-1">Control family spending limits and track monthly allowances</p>
        </div>

        <div class="flex items-center gap-3">
          <!-- Month Controls -->
          <div class="flex items-center bg-slate-100 rounded-lg p-1 border border-slate-200">
            <button nz-button nzType="text" nzSize="small" (click)="prevMonth()" class="text-slate-600">◀</button>
            <span class="px-3 font-semibold text-sm text-slate-800 whitespace-nowrap">{{ monthLabel() }}</span>
            <button nz-button nzType="text" nzSize="small" (click)="nextMonth()" class="text-slate-600">▶</button>
          </div>

          <button
            nz-button
            nzType="primary"
            (click)="openSetBudgetModal()"
            class="rounded-lg text-sm font-medium bg-emerald-600 hover:bg-emerald-500 border-0 shadow-xs">
            <span>➕ Set Budget</span>
          </button>
        </div>
      </div>

      <!-- Overview Stats Banner -->
      @if (budgetService.summary(); as s) {
        <div class="bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 rounded-2xl p-6 text-white shadow-sm space-y-4">
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-6">
            <div>
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider block">Total Allocated</span>
              <span class="text-2xl sm:text-3xl font-bold font-mono tracking-tight text-white mt-1 block">
                {{ s.totalAllocatedPaise | inrCurrency }}
              </span>
            </div>
            <div>
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider block">Total Spent</span>
              <span
                class="text-2xl sm:text-3xl font-bold font-mono tracking-tight mt-1 block"
                [ngClass]="s.totalSpentPaise > s.totalAllocatedPaise ? 'text-rose-400' : 'text-emerald-400'">
                {{ s.totalSpentPaise | inrCurrency }}
              </span>
            </div>
            <div>
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider block">Remaining Allowance</span>
              <span class="text-2xl sm:text-3xl font-bold font-mono tracking-tight text-white mt-1 block">
                {{ (s.totalAllocatedPaise - s.totalSpentPaise) | inrCurrency }}
              </span>
            </div>
          </div>

          <!-- Overall Progress Bar -->
          <div class="pt-2 border-t border-slate-700/60">
            <div class="flex items-center justify-between text-xs text-slate-300 mb-1.5 font-medium">
              <span>Overall Utilization</span>
              <span class="font-mono font-bold">{{ s.overallPercentageUsed }}%</span>
            </div>
            <nz-progress
              [nzPercent]="Math.min(100, s.overallPercentageUsed)"
              [nzStatus]="s.overallPercentageUsed > 100 ? 'exception' : (s.overallPercentageUsed > 80 ? 'active' : 'success')"
              [nzStrokeColor]="s.overallPercentageUsed > 100 ? '#ef4444' : (s.overallPercentageUsed > 80 ? '#f59e0b' : '#10b981')"
              [nzShowInfo]="false">
            </nz-progress>
          </div>
        </div>
      }

      <!-- Category Budgets Grid -->
      @if (!budgetService.summary()?.budgets?.length) {
        <nz-card class="rounded-xl border border-slate-200/80 text-center py-12">
          <nz-empty
            nzNotFoundImage="simple"
            nzNotFoundContent="No budgets configured for this month. Set spending limits to monitor expenses.">
            <button
              nz-button
              nzType="primary"
              (click)="openSetBudgetModal()"
              class="mt-3 bg-emerald-600 hover:bg-emerald-500 border-0 rounded-lg text-sm">
              Set Category Budget
            </button>
          </nz-empty>
        </nz-card>
      } @else {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          @for (b of budgetService.summary()?.budgets; track b.id) {
            <nz-card class="rounded-xl border border-slate-200/80 shadow-xs hover:shadow-md transition-shadow">
              <div class="flex items-start justify-between gap-3 mb-3">
                <div class="flex items-center gap-2.5">
                  <div
                    class="w-10 h-10 rounded-xl flex items-center justify-center text-lg shadow-xs"
                    [style.background-color]="(b.categoryColorHex || '#6366f1') + '15'">
                    {{ b.categoryIcon || '📁' }}
                  </div>
                  <div>
                    <h3 class="font-bold text-slate-900 text-base leading-tight">{{ b.categoryName }}</h3>
                    <span class="text-xs text-slate-400">
                      {{ b.rolloverEnabled ? 'Rollover Enabled' : 'Monthly Fixed' }}
                    </span>
                  </div>
                </div>

                <!-- Over budget badge -->
                @if (b.percentageUsed > 100) {
                  <nz-tag nzColor="error" class="rounded font-medium text-xs">Over Budget</nz-tag>
                } @else if (b.percentageUsed >= 80) {
                  <nz-tag nzColor="warning" class="rounded font-medium text-xs">Near Limit</nz-tag>
                } @else {
                  <nz-tag nzColor="success" class="rounded font-medium text-xs">On Track</nz-tag>
                }
              </div>

              <!-- Spent vs Limit -->
              <div class="space-y-2 mt-4">
                <div class="flex items-baseline justify-between">
                  <span class="text-xs text-slate-500 font-medium">Spent:</span>
                  <span class="font-mono font-bold text-sm text-slate-900">
                    {{ b.spentAmountPaise | inrCurrency }}
                    <span class="text-slate-400 font-normal">/ {{ b.allocatedAmountPaise | inrCurrency }}</span>
                  </span>
                </div>

                <!-- Progress Bar -->
                <nz-progress
                  [nzPercent]="Math.min(100, b.percentageUsed)"
                  [nzStatus]="b.percentageUsed > 100 ? 'exception' : (b.percentageUsed > 80 ? 'active' : 'success')"
                  [nzStrokeColor]="b.percentageUsed > 100 ? '#ef4444' : (b.percentageUsed > 80 ? '#f59e0b' : '#10b981')">
                </nz-progress>
              </div>

              <!-- Card Footer with Remaining and Actions -->
              <div class="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
                <div>
                  <span class="text-slate-400">Remaining: </span>
                  <span
                    class="font-mono font-semibold"
                    [ngClass]="b.allocatedAmountPaise - b.spentAmountPaise < 0 ? 'text-rose-600' : 'text-emerald-600'">
                    {{ (b.allocatedAmountPaise - b.spentAmountPaise) | inrCurrency }}
                  </span>
                </div>

                <div class="flex items-center gap-1">
                  <button
                    nz-button
                    nzType="text"
                    (click)="openEditBudgetModal(b)"
                    class="text-xs text-slate-500 hover:text-emerald-600 p-1">
                    Edit
                  </button>
                  <button
                    nz-button
                    nzType="text"
                    nzDanger
                    nz-popconfirm
                    nzPopconfirmTitle="Remove budget for this category?"
                    (nzOnConfirm)="deleteBudget(b.id)"
                    class="text-xs p-1 text-slate-400 hover:text-rose-600">
                    ✕
                  </button>
                </div>
              </div>
            </nz-card>
          }
        </div>
      }

      <!-- Set Budget Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        [nzTitle]="isEditing ? 'Edit Category Budget' : 'Set Category Budget'"
        [nzOkText]="'Save Budget'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closeModal()"
        (nzOnOk)="submitBudget()">
        <ng-container *nzModalContent>
          <form [formGroup]="budgetForm" nz-form nzLayout="vertical" class="space-y-3 pt-2">
            <!-- Category -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Category</nz-form-label>
              <nz-form-control nzErrorTip="Category is required">
                <nz-select formControlName="categoryId" nzPlaceHolder="Select expense category">
                  @for (cat of expenseCategories(); track cat.id) {
                    <nz-option [nzValue]="cat.id" [nzLabel]="cat.icon ? cat.icon + ' ' + cat.name : cat.name"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <!-- Monthly Limit (₹) -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Monthly Budget Allowance (₹)</nz-form-label>
              <nz-form-control nzErrorTip="Amount must be 0 or greater">
                <nz-input-number
                  formControlName="amountRupees"
                  [nzMin]="0"
                  [nzStep]="500"
                  class="w-full">
                </nz-input-number>
              </nz-form-control>
            </nz-form-item>

            <!-- Rollover -->
            <div class="pt-2 flex items-center justify-between">
              <div>
                <span class="text-sm font-medium text-slate-800 block">Rollover Unspent Budget</span>
                <span class="text-xs text-slate-400">Carry over surplus savings into next month's allocation</span>
              </div>
              <nz-switch formControlName="rolloverEnabled"></nz-switch>
            </div>
          </form>
        </ng-container>
      </nz-modal>
    </div>
  `
})
export class BudgetsComponent implements OnInit {
  budgetService = inject(BudgetService);
  transactionService = inject(TransactionService);
  private fb = inject(FormBuilder);
  private msg = inject(NzMessageService);

  readonly Math = Math;

  currentMonth = new Date().getMonth() + 1;
  currentYear = new Date().getFullYear();

  isModalVisible = false;
  isEditing = false;
  isSubmitting = false;

  monthLabel = computed(() => {
    const d = new Date(this.currentYear, this.currentMonth - 1, 1);
    return d.toLocaleString('default', { month: 'long', year: 'numeric' });
  });

  expenseCategories = computed(() => {
    return this.transactionService.categories().filter(c => c.type === 1 || c.type === 'Expense');
  });

  budgetForm = this.fb.group({
    categoryId: ['', [Validators.required]],
    amountRupees: [0, [Validators.required, Validators.min(0)]],
    rolloverEnabled: [false]
  });

  ngOnInit(): void {
    this.transactionService.loadCategories().subscribe();
    this.loadData();
  }

  loadData(): void {
    this.budgetService.loadMonthlyBudgets(this.currentMonth, this.currentYear).subscribe();
  }

  prevMonth(): void {
    if (this.currentMonth === 1) {
      this.currentMonth = 12;
      this.currentYear--;
    } else {
      this.currentMonth--;
    }
    this.loadData();
  }

  nextMonth(): void {
    if (this.currentMonth === 12) {
      this.currentMonth = 1;
      this.currentYear++;
    } else {
      this.currentMonth++;
    }
    this.loadData();
  }

  openSetBudgetModal(): void {
    this.isEditing = false;
    this.budgetForm.reset({
      categoryId: this.expenseCategories()[0]?.id || '',
      amountRupees: 5000,
      rolloverEnabled: false
    });
    this.isModalVisible = true;
  }

  openEditBudgetModal(b: Budget): void {
    this.isEditing = true;
    this.budgetForm.setValue({
      categoryId: b.categoryId,
      amountRupees: b.allocatedAmountPaise / 100,
      rolloverEnabled: b.rolloverEnabled
    });
    this.isModalVisible = true;
  }

  closeModal(): void {
    this.isModalVisible = false;
  }

  submitBudget(): void {
    if (this.budgetForm.invalid) {
      Object.values(this.budgetForm.controls).forEach(c => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.budgetForm.value;
    const allocatedAmountPaise = Math.round(Number(val.amountRupees) * 100);

    this.isSubmitting = true;
    this.budgetService.setBudget({
      categoryId: val.categoryId!,
      month: this.currentMonth,
      year: this.currentYear,
      allocatedAmountPaise,
      rolloverEnabled: !!val.rolloverEnabled
    }).subscribe({
      next: () => {
        this.msg.success('Budget saved successfully');
        this.isSubmitting = false;
        this.isModalVisible = false;
      },
      error: err => {
        this.msg.error(err.error?.detail || 'Failed to save budget');
        this.isSubmitting = false;
      }
    });
  }

  deleteBudget(id: string): void {
    this.budgetService.deleteBudget(id, this.currentMonth, this.currentYear).subscribe({
      next: () => this.msg.success('Budget deleted'),
      error: () => this.msg.error('Failed to delete budget')
    });
  }
}

