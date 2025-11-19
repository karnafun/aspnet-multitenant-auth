import { useAuth } from '../contexts/AuthContext';
import api from '../api/axios';
import { useState } from 'react';

function DashboardPage() {
  const { user, logout } = useAuth();
  const [testResult, setTestResult] = useState('');

  const testProtectedEndpoint = async () => {
    try {
      const response = await api.get('/api/users/me');
      setTestResult('✅ Success: ' + JSON.stringify(response.data));
    } catch (error) {
      setTestResult('❌ Error: ' + error.message);
    }
  };

  return (
    <div className="min-h-screen p-8">
      <div className="max-w-4xl mx-auto">
        <div className="card">
          <h1 className="text-3xl font-bold mb-4">Dashboard</h1>
          <p className="text-text-secondary mb-6">Welcome back!</p>
          
          <div className="bg-primary-bg p-4 rounded mb-6">
            <h2 className="text-sm text-text-secondary mb-2">Your Info:</h2>
            <pre className="text-sm">
              {JSON.stringify(user, null, 2)}
            </pre>
          </div>

          <div className="flex gap-4">
            <button 
              onClick={testProtectedEndpoint} 
              className="btn btn-primary"
            >
              Test Protected Endpoint
            </button>
            
            <button onClick={logout} className="btn btn-secondary">
              Logout
            </button>
          </div>

          {testResult && (
            <div className="text-sm bg-primary-bg p-3 rounded mt-4 break-words">
              {testResult}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default DashboardPage;