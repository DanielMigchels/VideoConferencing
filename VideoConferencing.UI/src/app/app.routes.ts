import { Routes } from '@angular/router';
import { BaseLayout } from './layout/base-layout/base-layout';
import { Lobby } from './lobby/lobby';

export const routes: Routes = [{
  path: '',
  component: BaseLayout,
  children: [
    { path: '', component: Lobby },
  ]
},
{ path: '**', redirectTo: '', pathMatch: 'full' },
];