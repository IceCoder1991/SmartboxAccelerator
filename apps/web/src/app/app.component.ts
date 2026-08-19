import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'sbx-root',
  standalone: true,
  templateUrl: './app.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  protected readonly title = 'Smartboxx Client Accelerator';
}
