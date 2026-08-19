import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

interface NavigationItem { label: string; route: string; feature: string; permission: string }
interface Bootstrap {
  clientName: string; productName: string; navigation: NavigationItem[];
  featureFlags: Record<string, boolean>; themeTokens: Record<string, string>;
  logo: string; favicon: string; currentUser: { displayName: string; permissions: string[] };
}
interface ValueReport { metrics: { name: string; value: number; unit: string; classification: string }[]; dataQualityWarnings: string[]; disclaimer: string }

@Component({
  selector: 'sbx-root', standalone: true, imports: [CommonModule],
  templateUrl: './app.component.html', changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly bootstrap = signal<Bootstrap | null>(null);
  protected readonly error = signal(false);
  protected readonly valueReport = signal<ValueReport | null>(null);

  ngOnInit(): void {
    this.http.get<Bootstrap>('/api/platform/bootstrap').subscribe({
      next: data => {
        this.bootstrap.set(data);
        document.documentElement.style.setProperty('--primary', data.themeTokens['primary'] ?? '#006f70');
        document.title = data.productName;
        document.querySelector<HTMLLinkElement>('link[rel="icon"]')?.setAttribute('href', data.favicon);
        if (data.featureFlags['value']) this.http.get<ValueReport>('/api/value/report').subscribe(report => this.valueReport.set(report));
      },
      error: () => this.error.set(true),
    });
  }
}
