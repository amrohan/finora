import { Component, OnInit, signal, computed, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { ReactiveFormsModule, FormBuilder, Validators } from "@angular/forms";
import { NzCardModule } from "ng-zorro-antd/card";
import { NzButtonModule } from "ng-zorro-antd/button";
import { NzModalModule } from "ng-zorro-antd/modal";
import { NzFormModule } from "ng-zorro-antd/form";
import { NzInputModule } from "ng-zorro-antd/input";
import { NzSelectModule } from "ng-zorro-antd/select";
import { NzInputNumberModule } from "ng-zorro-antd/input-number";
import { NzDatePickerModule } from "ng-zorro-antd/date-picker";
import { NzProgressModule } from "ng-zorro-antd/progress";
import { NzTagModule } from "ng-zorro-antd/tag";
import { NzPopconfirmModule } from "ng-zorro-antd/popconfirm";
import { NzEmptyModule } from "ng-zorro-antd/empty";
import { NzMessageService } from "ng-zorro-antd/message";
import { InrCurrencyPipe } from "../../shared/pipes/inr-currency.pipe";
import { GoalService, SavingsGoal } from "../../core/services/goal.service";
import { AccountService } from "../../core/services/account.service";

@Component({
  selector: "app-goals",
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
    NzPopconfirmModule,
    NzEmptyModule,
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
            Savings Goals & Sinking Funds
          </h1>
          <p class="text-sm text-slate-500 mt-1">
            Capital allocations for emergency funds, vacations, education, and
            milestones
          </p>
        </div>

        <button
          nz-button
          nzType="primary"
          (click)="openAddGoalModal()"
          class="rounded-lg text-sm font-medium bg-emerald-600 hover:bg-emerald-500 border-0 shadow-xs flex items-center gap-1.5"
        >
          <span>New Savings Target</span>
        </button>
      </div>

      <!-- Total Accumulated Savings Banner -->
      <div
        class="bg-linear-to-r from-emerald-950 via-slate-900 to-slate-900 rounded-2xl p-6 text-white shadow-sm flex flex-col sm:flex-row sm:items-center justify-between gap-4"
      >
        <div>
          <span
            class="text-xs font-semibold text-emerald-400 uppercase tracking-wider block"
            >Total Accumulated Savings</span
          >
          <span
            class="text-3xl font-bold font-mono tracking-tight text-emerald-400 mt-1 block"
          >
            {{ goalService.totalSavingsPaise() | inrCurrency }}
          </span>
        </div>
        <div class="flex items-center gap-6 text-sm text-slate-300">
          <div>
            <span class="text-slate-400 text-xs block">Active Goals</span>
            <span class="font-bold text-lg text-white font-mono">{{
              goalService.goals().length
            }}</span>
          </div>
        </div>
      </div>

      <!-- Goals Grid -->
      @if (goalService.goals().length === 0) {
        <nz-card
          class="rounded-xl border border-slate-200/80 text-center py-12"
        >
          <nz-empty
            nzNotFoundImage="simple"
            nzNotFoundContent="No savings goals created yet. Set a target for an emergency fund, home deposit, or vacation."
          >
            <button
              nz-button
              nzType="primary"
              (click)="openAddGoalModal()"
              class="mt-3 bg-emerald-600 hover:bg-emerald-500 border-0 rounded-lg text-sm"
            >
              Create First Goal
            </button>
          </nz-empty>
        </nz-card>
      } @else {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          @for (goal of goalService.goals(); track goal.id) {
            <nz-card
              class="rounded-xl border border-slate-200/80 shadow-xs hover:shadow-md transition-shadow relative overflow-hidden"
            >
              <!-- Completed Banner Ribbon -->
              @if (goal.isCompleted) {
                <div class="absolute top-2 right-2">
                  <nz-tag
                    nzColor="success"
                    class="rounded font-bold text-xs m-0"
                  >
                    🏆 Goal Achieved!
                  </nz-tag>
                </div>
              }

              <!-- Card Header -->
              <div class="flex items-start justify-between gap-3 mb-2">
                <div>
                  <h3 class="font-bold text-slate-900 text-base leading-tight">
                    {{ goal.name }}
                  </h3>
                  @if (goal.targetDate) {
                    <span class="text-xs text-slate-400 font-mono"
                      >Target: {{ goal.targetDate }}</span
                    >
                  }
                </div>
              </div>

              <!-- Accumulated vs Target -->
              <div class="space-y-2 mt-4 pt-3 border-t border-slate-100">
                <div class="flex items-baseline justify-between text-xs">
                  <span class="text-slate-500">Saved:</span>
                  <span class="font-mono font-bold text-slate-900 text-sm">
                    {{ goal.currentAmountPaise | inrCurrency }}
                    <span class="text-slate-400 font-normal"
                      >/ {{ goal.targetAmountPaise | inrCurrency }}</span
                    >
                  </span>
                </div>

                <!-- Progress Bar & Milestones (Criterion #3) -->
                <nz-progress
                  [nzPercent]="Math.min(100, goal.progressPercentage)"
                  [nzStatus]="goal.isCompleted ? 'success' : 'active'"
                  [nzStrokeColor]="goal.isCompleted ? '#10b981' : '#0ea5e9'"
                >
                </nz-progress>

                <div
                  class="flex items-center justify-between text-[11px] text-slate-400"
                >
                  <div class="flex items-center gap-1">
                    @if (goal.progressPercentage >= 50 && !goal.isCompleted) {
                      <nz-tag nzColor="blue" class="rounded text-[10px] m-0"
                        >50% Halfway</nz-tag
                      >
                    }
                    @if (goal.progressPercentage >= 75 && !goal.isCompleted) {
                      <nz-tag nzColor="cyan" class="rounded text-[10px] m-0"
                        >75% Almost There</nz-tag
                      >
                    }
                  </div>
                  <span class="font-mono font-semibold"
                    >{{ goal.progressPercentage }}%</span
                  >
                </div>
              </div>

              <!-- Actions -->
              <div
                class="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between gap-2"
              >
                <button
                  nz-button
                  nzType="primary"
                  nzSize="small"
                  (click)="openContributeModal(goal)"
                  class="rounded-md text-xs bg-emerald-600 hover:bg-emerald-500 border-0 font-medium"
                >
                  Contribute Funds
                </button>

                <button
                  nz-button
                  nzType="text"
                  nzDanger
                  nz-popconfirm
                  nzPopconfirmTitle="Delete this savings goal?"
                  (nzOnConfirm)="deleteGoal(goal.id)"
                  class="text-xs p-1 text-slate-400 hover:text-rose-600"
                >
                  ✕
                </button>
              </div>
            </nz-card>
          }
        </div>
      }

      <!-- Add Goal Modal -->
      <nz-modal
        [(nzVisible)]="isAddModalVisible"
        nzTitle="Create Savings Target"
        [nzOkText]="'Save Target'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closeAddModal()"
        (nzOnOk)="submitAddGoal()"
      >
        <ng-container *nzModalContent>
          <form
            [formGroup]="goalForm"
            nz-form
            nzLayout="vertical"
            class="space-y-3 pt-2"
          >
            <!-- Name -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Goal Name</nz-form-label>
              <nz-form-control nzErrorTip="Goal name is required">
                <input
                  nz-input
                  formControlName="name"
                  placeholder="e.g. 6-Month Emergency Fund, Diwali Trip, House Deposit"
                />
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-3">
              <!-- Target Amount (₹) -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Target Amount (₹)</nz-form-label>
                <nz-form-control nzErrorTip="Amount must be positive">
                  <nz-input-number
                    formControlName="targetRupees"
                    [nzMin]="1"
                    [nzStep]="5000"
                    class="w-full"
                  >
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Target Date -->
              <nz-form-item class="mb-3">
                <nz-form-label>Target Completion Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker
                    formControlName="targetDate"
                    class="w-full"
                  ></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Contribute Funds Modal (Criterion #3) -->
      <nz-modal
        [(nzVisible)]="isContributeModalVisible"
        [nzTitle]="'Contribute to: ' + (selectedGoal?.name || '')"
        [nzOkText]="'Transfer to Goal'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closeContributeModal()"
        (nzOnOk)="submitContribute()"
      >
        <ng-container *nzModalContent>
          <div
            class="bg-emerald-50 border border-emerald-200 rounded-lg p-3 text-xs text-emerald-800 mb-3"
          >
            💡 <strong>Capital Allocation Invariant:</strong> Contributions are
            transfers of wealth into sinking funds. They do
            <strong>not</strong> count as monthly expenditures in your household
            budgets.
          </div>

          <form
            [formGroup]="contribForm"
            nz-form
            nzLayout="vertical"
            class="space-y-3"
          >
            <!-- Source Account -->
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Funding Account (From)</nz-form-label>
              <nz-form-control nzErrorTip="Account is required">
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

            <div class="grid grid-cols-2 gap-3">
              <!-- Amount (₹) -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired
                  >Contribution Amount (₹)</nz-form-label
                >
                <nz-form-control nzErrorTip="Amount must be positive">
                  <nz-input-number
                    formControlName="amountRupees"
                    [nzMin]="1"
                    [nzStep]="1000"
                    class="w-full"
                  >
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <!-- Date -->
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Contribution Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker
                    formControlName="contributionDate"
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
                  placeholder="e.g. Monthly savings contribution"
                />
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
    </div>
  `,
})
export class GoalsComponent implements OnInit {
  goalService = inject(GoalService);
  accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private msg = inject(NzMessageService);

  readonly Math = Math;

  isAddModalVisible = false;
  isContributeModalVisible = false;
  isSubmitting = false;

  selectedGoal: SavingsGoal | null = null;

  goalForm = this.fb.group({
    name: ["", [Validators.required]],
    targetRupees: [100000, [Validators.required, Validators.min(1)]],
    targetDate: [null],
  });

  contribForm = this.fb.group({
    sourceAccountId: ["", [Validators.required]],
    amountRupees: [5000, [Validators.required, Validators.min(1)]],
    contributionDate: [new Date(), [Validators.required]],
    notes: [""],
  });

  ngOnInit(): void {
    this.accountService.loadAccounts().subscribe();
    this.goalService.loadGoals().subscribe();
  }

  openAddGoalModal(): void {
    this.goalForm.reset({
      name: "",
      targetRupees: 100000,
      targetDate: null,
    });
    this.isAddModalVisible = true;
  }

  closeAddModal(): void {
    this.isAddModalVisible = false;
  }

  submitAddGoal(): void {
    if (this.goalForm.invalid) {
      Object.values(this.goalForm.controls).forEach((c) => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.goalForm.value;
    const targetAmountPaise = Math.round(Number(val.targetRupees) * 100);
    const dateVal = val.targetDate ? new Date(val.targetDate) : null;
    const formattedDate = dateVal
      ? dateVal.toISOString().split("T")[0]
      : undefined;

    this.isSubmitting = true;
    this.goalService
      .createGoal({
        name: val.name!,
        targetAmountPaise,
        targetDate: formattedDate,
      })
      .subscribe({
        next: () => {
          this.msg.success("Savings target created successfully");
          this.isSubmitting = false;
          this.isAddModalVisible = false;
        },
        error: (err) => {
          this.msg.error(err.error?.detail || "Failed to create goal");
          this.isSubmitting = false;
        },
      });
  }

  openContributeModal(goal: SavingsGoal): void {
    this.selectedGoal = goal;
    this.contribForm.reset({
      sourceAccountId: this.accountService.accounts()[0]?.id || "",
      amountRupees: 5000,
      contributionDate: new Date(),
      notes: `Savings allocation for ${goal.name}`,
    });
    this.isContributeModalVisible = true;
  }

  closeContributeModal(): void {
    this.isContributeModalVisible = false;
    this.selectedGoal = null;
  }

  submitContribute(): void {
    if (this.contribForm.invalid || !this.selectedGoal) {
      Object.values(this.contribForm.controls).forEach((c) => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.contribForm.value;
    const amountPaise = Math.round(Number(val.amountRupees) * 100);
    const dateVal = val.contributionDate
      ? new Date(val.contributionDate)
      : new Date();
    const formattedDate = dateVal.toISOString().split("T")[0];

    this.isSubmitting = true;
    this.goalService
      .contribute(this.selectedGoal.id, {
        sourceAccountId: val.sourceAccountId!,
        amountPaise,
        contributionDate: formattedDate,
        notes: val.notes?.trim() || undefined,
      })
      .subscribe({
        next: () => {
          this.msg.success("Funds contributed to savings target");
          this.isSubmitting = false;
          this.isContributeModalVisible = false;
          this.accountService.loadAccounts().subscribe();
        },
        error: (err) => {
          this.msg.error(err.error?.detail || "Failed to contribute funds");
          this.isSubmitting = false;
        },
      });
  }

  deleteGoal(id: string): void {
    this.goalService.deleteGoal(id).subscribe({
      next: () => this.msg.success("Savings target deleted"),
      error: () => this.msg.error("Failed to delete goal"),
    });
  }
}
