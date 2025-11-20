import { Navigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

export default function AdminRoute({ children }) {
  const { user } = useAuth();

  // Not logged in → yeet
  if (!user) return <Navigate to="/" replace />;

  // Not admin → yeet to /projects
  const isAdmin = user.roles?.includes("Admin");
  if (!isAdmin) return <Navigate to="/projects" replace />;

  return children;
}
