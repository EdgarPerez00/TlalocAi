import { Outlet } from 'react-router-dom'
import AppNavbar from '../components/AppNavbar'

function MainLayout() {
  return (
    <div className="app-shell">
      <AppNavbar />
      <main className="container-fluid px-4 py-4">
        <Outlet />
      </main>
    </div>
  )
}

export default MainLayout
