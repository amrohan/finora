import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-setup-admin',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    NzFormModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzAlertModule
  ],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-slate-100 p-4">
      <nz-card class="w-full max-w-lg shadow-lg rounded-2xl border-0">
        <div class="text-center mb-6">
          <div class="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-emerald-600 text-white font-bold text-2xl mb-3 shadow-md">
            🛡️
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Initial System Setup</h1>
          <p class="text-sm text-slate-500 mt-1">Configure the primary household and administrator account</p>
        </div>

        @if (errorMessage()) {
          <nz-alert
            nzType="error"
            [nzMessage]="errorMessage()!"
            nzShowIcon
            class="mb-6 rounded-lg">
          </nz-alert>
        }

        <form nz-form [formGroup]="setupForm" (ngSubmit)="onSubmit()" nzLayout="vertical">
          <nz-form-item>
            <nz-form-label nzRequired>Setup Token</nz-form-label>
            <nz-form-control nzErrorTip="Server setup token is required">
              <input
                nz-input
                formControlName="setupToken"
                type="password"
                placeholder="Environment INITIAL_SETUP_TOKEN"
                class="rounded-lg h-10 font-mono text-sm" />
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Household Name</nz-form-label>
            <nz-form-control nzErrorTip="Household name is required">
              <input
                nz-input
                formControlName="householdName"
                placeholder="e.g. Sharma Family"
                class="rounded-lg h-10" />
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Admin Full Name</nz-form-label>
            <nz-form-control nzErrorTip="Full name is required">
              <input
                nz-input
                formControlName="fullName"
                placeholder="Rohan Sharma"
                class="rounded-lg h-10" />
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Admin Email</nz-form-label>
            <nz-form-control nzErrorTip="Valid email is required">
              <input
                nz-input
                formControlName="email"
                type="email"
                placeholder="admin@example.com"
                class="rounded-lg h-10" />
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Admin Password</nz-form-label>
            <nz-form-control nzErrorTip="Minimum 8 characters with uppercase, lowercase, digit and symbol">
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
            [nzLoading]="isLoading()"
            class="mt-4 h-11 bg-emerald-600 hover:bg-emerald-500 border-0 font-medium rounded-lg text-base shadow-sm">
            Complete Initial Setup
          </button>
        </form>

        <div class="mt-6 text-center text-xs text-slate-500">
          <a routerLink="/auth/login" class="text-slate-600 hover:underline">
            ← Back to Sign In
          </a>
        </div>
      </nz-card>
    </div>
  `
})
export class SetupAdminComponent {
  setupForm: FormGroup;
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.setupForm = this.fb.group({
      setupToken: ['', [Validators.required]],
      householdName: ['', [Validators.required, Validators.maxLength(100)]],
      fullName: ['', [Validators.required, Validators.maxLength(128)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]]
    });
  }

  onSubmit(): void {
    if (this.setupForm.invalid) {
      Object.values(this.setupForm.controls).forEach(control => {
        if (control.invalid) {
          control.markAsDirty();
          control.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.registerInitialAdmin(this.setupForm.value).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: err => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.detail || 'Setup failed. Please check your setup token.');
      }
    });
  }
}

