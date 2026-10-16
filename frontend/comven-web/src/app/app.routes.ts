import { Routes } from '@angular/router';
import { Statement } from './pages/statement/statement';
import { TestScenarios } from './pages/test-scenarios/test-scenarios';

export const routes: Routes = [
    {
        path: '',
        component: Statement
    },
    {
        path: 'test-scenarios',
        component: TestScenarios
    },
    {
        path: '**',
        redirectTo: ''
    }
];
