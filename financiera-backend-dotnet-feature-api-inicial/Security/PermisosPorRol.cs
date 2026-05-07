using ApiEjemplo.Enums;

namespace ApiEjemplo.Security
{
    public static class PermisosPorRol
    {
        public static List<string> Obtener(RolUsuario rol)
        {
            return rol switch
            {
                RolUsuario.ADMIN => new List<string>
                {
                    "dashboard:view",
                    "credits:view",
                    "collections:view",
                    "reports:view"
                },

                RolUsuario.COBRADOR => new List<string>
                {
                    "dashboard:view",
                    "collections:view"
                },

                RolUsuario.CLIENTE => new List<string>
                {
                    "dashboard:view"
                },

                _ => new List<string>()
            };
        }
    }
}