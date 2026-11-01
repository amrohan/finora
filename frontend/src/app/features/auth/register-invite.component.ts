import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { AuthService } from '../../core/services/auth.service';
import { HouseholdService } from '../../core/services/household.service';

@Component({
  selector: 'app-register-invite',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    NzFormModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzAlertModule,
    NzSpinModule
  ],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-slate-100 p-4">
      <nz-card class="w-full max-w-md shadow-lg rounded-2xl border-0">
        @if (isVerifyingToken()) {
          <div class="text-center py-12">
            <nz-spin nzSimple></nz-spin>
            <p class="text-sm text-slate-500 mt-4">Verifying invitation link...</p>
          </div>
        } @else if (inviteDetails()) {
          <div class="text-center mb-6">
            <div class="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-emerald-600 text-white font-bold text-2xl mb-3 shadow-md">
              🤝
            </div>
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Join {{ inviteDetails().householdName }}</h1>
            <p class="text-sm text-slate-500 mt-1">Invited as <span class="font-medium text-slate-800">{{ inviteDetails().role }}</span> ({{ inviteDetails().email }})</p>
          </div>

          @if (errorMessage()) {
            <nz-alert
              nzType="error"
              [nzMessage]="errorMessage()!"
              nzShowIcon
              class="mb-6 rounded-lg">
            </nz-alert>
          }

          <form nz-form [formGroup]="inviteForm" (ngSubmit)="onSubmit()" nzLayout="vertical">
            <nz-form-item>
              <nz-form-label nzRequired>Your Full Name</nz-form-label>
              <nz-form-control nzErrorTip="Full name is required">
                <input
                  nz-input
                  formControlName="fullName"
                  placeholder="Priya Sharma"
                  class="rounded-lg h-10" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Create Password</nz-form-label>
              <nz-form-control nzErrorTip="Minimum 8 characters required">
                <input
                  nz-input
                  formControlName="password"
                  type="password"
                  placeholder="••••••••"
                  class="rounded-lg h-10" />
              </nz-form-control>
            </nz-form-item>

            <button
              nz-button
              nzType="primary"
              nzBlock
              [nzLoading]="isSubmitting()"
              class="mt-4 h-11 bg-emerald-600 hover:bg-emerald-500 border-0 font-medium rounded-lg text-base shadow-sm">
              Accept Invitation & Join
            </button>
          </form>
        } @else {
          <div class="text-center py-6">
            <nz-alert
              nzType="error"
              nzMessage="Invalid or Expired Invitation"
              nzDescription="This invitation link is invalid or has already been used. Please request a new invitation from your household administrator."
              nzShowIcon
              class="rounded-lg text-left mb-6">
            </nz-alert>
            <a routerLink="/auth/login" class="text-emerald-600 font-medium hover:underline text-sm">
              Return to Sign In
            </a>
          </div>
        }
      </nz-card>
    </div>
  `
})
export class RegisterInviteComponent implements OnInit {
  token = '';
  inviteDetails = signal<any>(null);
  isVerifyingToken = signal(true);
  isSubmitting = signal(false);
  errorMessage = signal<string | null>(null);
  inviteForm: FormGroup;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder,
    private authService: AuthService,
    private householdService: HouseholdService
  ) {
    this.inviteForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(128)]],
      password: ['', [Validators.required, Validators.minLength(8)]]
    });
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') || '';
    if (!this.token) {
      this.isVerifyingToken.set(false);
      return;
    }

    this.householdService.inspectInvitation(this.token).subscribe({
      next: data => {
        this.inviteDetails.set(data);
        this.isVerifyingToken.set(false);
      },
      error: () => {
        this.isVerifyingToken.set(false);
      }
    });
  }

  onSubmit(): void {
    if (this.inviteForm.invalid) {
      Object.values(this.inviteForm.controls).forEach(c => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const payload = {
      fullName: this.inviteForm.value.fullName,
      password: this.inviteForm.value.password,
      inviteToken: this.token
    };

    this.authService.registerWithInvite(payload).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: err => {
        this.isSubmitting.set(false);
        this.errorMessage.set(err.error?.detail || 'Failed to complete registration.');
      }
    });
  }
}

