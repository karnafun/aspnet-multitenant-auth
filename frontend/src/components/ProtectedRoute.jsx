import { Navigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

function ProtectedRoute({ children }) {
  const { accessToken, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="text-4xl mb-4">⏳</div>
          <p className="text-text-secondary">Loading...</p>
        </div>
      </div>
    );
  }

  if (!accessToken) {
    return <Navigate to="/" replace />;
  }

  return children;
}

export default ProtectedRoute;