import { Routes } from '@angular/router';
import { Statement } from './pages/statement/statement';

export const routes: Routes = [
    {
        path: '',
        component: Statement
    },
    {
        path: '**',
        redirectTo: ''
    }
];
