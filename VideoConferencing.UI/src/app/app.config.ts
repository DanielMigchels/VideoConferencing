import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideIcons } from '@ng-icons/core';
import { routes } from './app.routes';
import { heroPhoneSolid, heroPlusSolid, heroTrashSolid, heroUserSolid } from '@ng-icons/heroicons/solid';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideIcons({heroPlusSolid, heroUserSolid, heroTrashSolid, heroPhoneSolid})
  ]
};
