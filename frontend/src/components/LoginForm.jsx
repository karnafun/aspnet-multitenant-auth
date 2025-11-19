import { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';

function LoginForm() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [tenant, setTenant] = useState('');
  const [error, setError] = useState('');
  
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    
    try {
      await login(email, password, tenant);
      navigate('/dashboard'); // ← Add this
    } catch (err) {
      setError(err.message || 'Login failed');
    }
  };
  return (
    <div className="card max-w-md mx-auto">
      <h2 className="text-2xl font-bold mb-6 text-center">Login</h2>
      
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm text-text-secondary mb-2">
            Email
          </label>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="input"
            placeholder="admin@tenanta.com"
            required
          />
        </div>

        <div>
          <label className="block text-sm text-text-secondary mb-2">
            Password
          </label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="input"
            placeholder="••••••••"
            required
          />
        </div>

        <div>
          <label className="block text-sm text-text-secondary mb-2">
            Tenant
          </label>
          <input
            type="text"
            value={tenant}
            onChange={(e) => setTenant(e.target.value)}
            className="input"
            placeholder="tenanta"
            required
          />
        </div>

        {error && (
          <div className="text-error text-sm text-center">
            {error}
          </div>
        )}

        <button type="submit" className="btn btn-primary w-full">
          Login
        </button>
      </form>
    </div>
  );
}

export default LoginForm;