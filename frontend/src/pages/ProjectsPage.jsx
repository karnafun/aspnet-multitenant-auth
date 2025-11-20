import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { projectsApi } from '../api/projectsApi';

function ProjectsPage() {
  const [projects, setProjects] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    loadProjects();
  }, []);

  const loadProjects = async () => {
    try {
      setIsLoading(true);
      const data = await projectsApi.getAllProjects();
      setProjects(data);
    } catch (err) {
      setError(err.response?.data?.detail || 'Failed to load projects');
    } finally {
      setIsLoading(false);
    }
  };

  const handleProjectClick = (projectId) => {
    navigate(`/projects/${projectId}`);
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="text-4xl mb-4">⏳</div>
          <p className="text-text-secondary">Loading projects...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="card max-w-md">
          <p className="text-error">{error}</p>
          <button onClick={loadProjects} className="btn btn-primary mt-4">
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen p-8">
      <div className="max-w-6xl mx-auto">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-3xl font-bold">Projects</h1>
          <button 
            onClick={() => navigate('/projects/new')}
            className="btn btn-primary"
          >
            + New Project
          </button>
        </div>

        {projects.length === 0 ? (
          <div className="card text-center py-12">
            <p className="text-text-secondary mb-4">No projects yet</p>
            <button 
              onClick={() => navigate('/projects/new')}
              className="btn btn-primary"
            >
              Create Your First Project
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {projects.map((project) => (
              <div
                key={project.id}
                onClick={() => handleProjectClick(project.id)}
                className="card cursor-pointer hover:border-accent-green transition-colors"
              >
                <h3 className="text-xl font-semibold mb-2">{project.title}</h3>
                
                {project.description && (
                  <p className="text-text-secondary text-sm mb-4 line-clamp-2">
                    {project.description}
                  </p>
                )}

                <div className="flex items-center justify-between text-sm">
                  <span className={`px-2 py-1 rounded ${
                    project.status === 'Open' ? 'bg-accent-green text-primary-bg' :
                    project.status === 'InProgress' ? 'bg-yellow-500 text-primary-bg' :
                    'bg-text-secondary text-primary-bg'
                  }`}>
                    {project.status}
                  </span>
                  {console.log(project)}
                  {project.assignedTo && (
                    <span className="text-text-secondary">
                      {project.assignedTo}
                      
                    </span>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export default ProjectsPage;